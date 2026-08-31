using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using EFCore.Dashboard.Core;
using EFCore.Dashboard.EntityFrameworkCore;
using EFCore.Dashboard.Web;

namespace EFCore.Dashboard.Pages.Admin;

[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class ResourceModel(
    IDashboardResourceProvider resources,
    IDashboardRepository repository,
    IDashboardValueConverter converter) : PageModel
{
    public DashboardResource? Resource { get; private set; }
    public DashboardPage? Result { get; private set; }
    public IReadOnlyList<DashboardField> Columns { get; private set; } = [];
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Sort { get; set; }
    [BindProperty(SupportsGet = true)] public bool Desc { get; set; }
    [BindProperty(SupportsGet = true)] public bool Export { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;
    [BindProperty(SupportsGet = true)] public string? FilterField { get; set; }
    [BindProperty(SupportsGet = true)] public string? FilterValue { get; set; }
    public DashboardField? ActiveFilter { get; private set; }
    public List<DeleteReferenceLink> DeleteReferences { get; } = [];
    private object? ConvertedFilterValue { get; set; }

    public async Task<IActionResult> OnGetAsync(string resourceName, CancellationToken cancellationToken = default)
    {
        PageNumber = Math.Max(1, PageNumber);
        if (!LoadResource(resourceName)) return NotFound();

        if (Export) return File(CreateCsvBytes(await repository.ExportAsync(Resource!, Query(), cancellationToken: cancellationToken)), "text/csv; charset=utf-8", $"{Resource!.Slug}.csv");

        Result = await repository.QueryAsync(Resource!, Query(PageNumber, PageSize), cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnGetImageAsync(
        string resourceName,
        string id,
        string field,
        CancellationToken cancellationToken = default)
    {
        var resource = resources.Find(resourceName);
        var imageField = resource?.Fields.OfType<BinaryField>().FirstOrDefault(candidate =>
            !candidate.Hidden && !candidate.HiddenInList &&
            candidate.Name.Equals(field, StringComparison.OrdinalIgnoreCase));
        if (resource is null || imageField is null ||
            !converter.TryConvert(resource.Key, id, out var key, out _) || key is null)
            return NotFound();

        var item = await repository.FindAsync(resource, key, cancellationToken);
        var bytes = item is null ? null : Value(item, imageField) as byte[];
        var contentType = DashboardImages.GetContentType(bytes);
        return bytes is null || contentType is null ? NotFound() : File(bytes, contentType);
    }

    public async Task<IActionResult> OnPostBulkDeleteAsync(
        string resourceName,
        string[]? selectedIds,
        CancellationToken cancellationToken = default)
    {
        PageNumber = Math.Max(1, PageNumber);
        if (!LoadResource(resourceName)) return NotFound();
        selectedIds ??= [];
        if (selectedIds.Length == 0)
            ModelState.AddModelError(string.Empty, "Select at least one record to delete.");

        var keys = new List<object>(selectedIds.Length);
        foreach (var id in selectedIds.Distinct(StringComparer.Ordinal))
        {
            if (!converter.TryConvert(Resource!.Key, id, out var key, out _) || key is null)
            {
                ModelState.AddModelError(string.Empty, $"'{id}' is not a valid {Resource.Key.Label}.");
                continue;
            }

            var blockers = await repository.GetDeleteBlockersAsync(Resource, key, cancellationToken);
            if (blockers.Count > 0)
            {
                var references = DeleteReferenceLink.CreateMany(resources, id, id, blockers);
                DeleteReferences.AddRange(references.Links);
                if (!references.Complete)
                    ModelState.AddModelError(string.Empty, $"'{id}' could not be deleted because related records exist.");
                continue;
            }
            keys.Add(key);
        }

        if (!ModelState.IsValid || DeleteReferences.Count > 0)
        {
            Result = await repository.QueryAsync(Resource!, Query(PageNumber, PageSize), cancellationToken);
            return Page();
        }

        await repository.DeleteManyAsync(Resource!, keys, cancellationToken);
        return RedirectToPage("/Admin/Resource", new
        {
            resourceName = Resource!.Slug,
            q = Q,
            sort = Sort,
            desc = Desc,
            pageNumber = PageNumber,
            pageSize = PageSize,
            filterField = FilterField,
            filterValue = FilterValue
        });
    }

    public object? Value(object item, DashboardField field) => EfDashboardRepository.GetValue(item, field);

    public string KeyValue(object item) => converter.Format(Resource!.Key, Value(item, Resource.Key));

    public bool HasImage(object item, DashboardField field)
        => field is BinaryField && DashboardImages.GetContentType(Value(item, field) as byte[]) is not null;

    public string? ImageUrl(object item, DashboardField field)
        => field is TextField { Editor: DashboardEditors.ImageUrl }
            ? DashboardImages.GetSafeRemoteUrl(Value(item, field) as string)
            : null;

    public bool? BooleanValue(object item, DashboardField field)
        => field is BooleanField ? Value(item, field) as bool? : null;

    public string Display(object item, DashboardField field)
    {
        if (field is RelationField relation)
            return repository.TryGetRelatedValue(item, relation, out var relatedValue)
                ? FormatDisplay(relatedValue)
                : "—";
        var value = Value(item, field);
        return FormatDisplay(value);
    }

    public string Tooltip(object item, DashboardField field)
    {
        if (field is not RelationField) return Display(item, field);
        var value = Value(item, field);
        var raw = value is null ? "null" : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null";
        return $"{field.Name}: {raw}";
    }

    public bool IsSortable(DashboardField field) => field is not RelationField;

    private bool LoadResource(string resourceName)
    {
        Resource = resources.Find(resourceName);
        if (Resource is null) return false;
        Columns = Resource.Fields
            .Where(field => !field.Hidden && !field.HiddenInList)
            .OrderBy(field => Resource.EntityType.GetProperty(field.Name)?.MetadataToken ?? int.MaxValue)
            .ToArray();
        Sort ??= Resource.DisplayField is RelationField ? Resource.Key.Name : Resource.DisplayField?.Name ?? Resource.Key.Name;
        if (Resource.Fields.FirstOrDefault(x => x.Name.Equals(Sort, StringComparison.OrdinalIgnoreCase)) is RelationField)
            Sort = Resource.Key.Name;
        ActiveFilter = Resource.Fields.OfType<RelationField>().FirstOrDefault(x =>
            x.Name.Equals(FilterField, StringComparison.OrdinalIgnoreCase));
        if (ActiveFilter is null || string.IsNullOrWhiteSpace(FilterValue) ||
            !converter.TryConvert(ActiveFilter, FilterValue, out var converted, out _))
        {
            ActiveFilter = null;
            FilterField = null;
            FilterValue = null;
        }
        else
        {
            FilterField = ActiveFilter.Name;
            ConvertedFilterValue = converted;
        }
        return true;
    }

    private DashboardQuery Query(int page = 1, int pageSize = 20)
        => new(Q, Sort, Desc, page, pageSize, Columns.Select(x => x.Name).ToArray(), FilterField, ConvertedFilterValue);

    private static string FormatDisplay(object? value)
    {
        if (value is null) return "—";
        if (value is byte[]) return "[Image]";
        if (value is bool b) return b ? "Yes" : "No";
        if (value is DateTime dt) return dt.ToString("dd MMM yyyy, HH:mm", CultureInfo.CurrentCulture);
        if (value is DateTimeOffset dto) return dto.ToString("dd MMM yyyy, HH:mm", CultureInfo.CurrentCulture);
        return Convert.ToString(value, CultureInfo.CurrentCulture) ?? "—";
    }

    private byte[] CreateCsvBytes(IReadOnlyList<object> items)
    {
        var rows = new List<IReadOnlyList<string>>();
        rows.Add(Columns.Select(x => x.Label).ToArray());
        foreach (var item in items)
            rows.Add(Columns.Select(x => Display(item, x)).ToArray());
        return Encoding.UTF8.GetBytes("\uFEFF" + DashboardCsv.Serialize(rows) + "\r\n");
    }
}
