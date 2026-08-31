using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using EFCore.Dashboard.Core;
using EFCore.Dashboard.EntityFrameworkCore;
using EFCore.Dashboard.Web;

namespace EFCore.Dashboard.Pages.Admin;

[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class EditModel(
    IDashboardResourceProvider resources,
    IDashboardRepository repository,
    IDashboardValueConverter converter) : PageModel
{
    /// <summary>Maximum size for uploaded binary values. Existing values are unaffected.</summary>
    public const long MaxBinaryUploadBytes = 10 * 1024 * 1024;
    private const string RemovePrefix = "__remove_";
    private const string OffsetPrefix = "__offset_";

    public DashboardResource? Resource { get; private set; }
    public object? Entity { get; private set; }
    public string? Id { get; private set; }
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    [BindProperty(SupportsGet = true)] public string? ParentField { get; set; }
    [BindProperty(SupportsGet = true)] public string? ParentValue { get; set; }
    public bool IsNew => string.IsNullOrWhiteSpace(Id);
    public IReadOnlyList<DashboardField> EditableFields { get; private set; } = [];
    public Dictionary<string, IReadOnlyList<DashboardLookupOption>> Relationships { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Maps relation field names to the label of their related resource for lookup placeholders.</summary>
    public Dictionary<string, string> RelatedLabels { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Child resources reachable from this record through one-to-many relationships.</summary>
    public List<RelatedRecordLink> RelatedRecords { get; } = [];
    public List<DeleteReferenceLink> DeleteReferences { get; } = [];
    private Dictionary<string, string?> PostedValues { get; } = new(StringComparer.OrdinalIgnoreCase);
    private object? Key { get; set; }

    public async Task<IActionResult> OnGetAsync(string resourceName, string? id, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(resourceName, id, cancellationToken)) return NotFound();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string resourceName, string? id, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(resourceName, id, cancellationToken)) return NotFound();
        var form = await Request.ReadFormAsync(cancellationToken);
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in EditableFields.Where(x => !x.ReadOnly))
        {
            if (field is BinaryField binary)
            {
                await ApplyBinaryAsync(binary, form, values, cancellationToken);
                continue;
            }

            var raw = field is BooleanField ? (form.ContainsKey(field.Name) ? "true" : "false") : form[field.Name].ToString();
            if (field is EnumField enumField && IsFlagsEnum(enumField) && string.IsNullOrWhiteSpace(raw) && field.Required)
                raw = "0";
            PostedValues[field.Name] = raw;
            if (IsDateTimeOffset(field))
            {
                var offset = form[OffsetInputName(field)].ToString();
                PostedValues[OffsetInputName(field)] = offset;
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    if (string.IsNullOrWhiteSpace(offset))
                    {
                        ModelState.AddModelError(field.Name, $"{field.Label} requires a UTC offset.");
                        continue;
                    }
                    raw += offset;
                }
            }
            if (!converter.TryConvert(field, raw, out var converted, out var error))
            {
                ModelState.AddModelError(field.Name, error!);
                continue;
            }
            if (field.MaxLength is not null && converted is string text && text.Length > field.MaxLength)
            {
                ModelState.AddModelError(field.Name, $"{field.Label} must be {field.MaxLength} characters or fewer.");
                continue;
            }
            values[field.Name] = converted;
        }

        if (!ModelState.IsValid)
        {
            await PreservePostedRelationshipsAsync(cancellationToken);
            return Page();
        }

        if (IsNew)
            await repository.CreateAsync(Resource!, values, cancellationToken);
        else
            await repository.UpdateAsync(Resource!, Key!, values, cancellationToken);

        return RedirectToResource();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string resourceName, string? id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id) || !await LoadAsync(resourceName, id, cancellationToken)) return NotFound();
        var blockers = await repository.GetDeleteBlockersAsync(Resource!, Key!, cancellationToken);
        if (blockers.Count > 0)
        {
            var label = converter.Format(Resource!.DisplayField ?? Resource!.Key, EfDashboardRepository.GetValue(Entity!, Resource!.DisplayField ?? Resource!.Key));
            var references = DeleteReferenceLink.CreateMany(resources, label, id, blockers);
            DeleteReferences.AddRange(references.Links);
            if (!references.Complete)
                ModelState.AddModelError(string.Empty, $"'{label}' could not be deleted because related records exist.");
            return Page();
        }
        await repository.DeleteAsync(Resource!, Key!, cancellationToken);
        return RedirectToResource();
    }

    public string RemoveInputName(BinaryField field) => RemovePrefix + field.Name;

    private IActionResult RedirectToResource()
        => !string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl)
            ? LocalRedirect(ReturnUrl)
            : RedirectToPage("/Admin/Resource", new { resourceName = Resource!.Slug });

    public bool HasBinaryValue(BinaryField field)
    {
        if (Entity is null) return false;
        var value = EfDashboardRepository.GetValue(Entity, field);
        return value is byte[] bytes && bytes.Length > 0;
    }

    public string? BinaryPreview(BinaryField field)
    {
        if (Entity is null) return null;
        return DashboardImages.ToDataUrl(EfDashboardRepository.GetValue(Entity, field) as byte[]);
    }

    private async Task ApplyBinaryAsync(BinaryField field, IFormCollection form, Dictionary<string, object?> values, CancellationToken cancellationToken)
    {
        var file = form.Files.GetFile(field.Name);
        if (file is not null && file.Length > 0)
        {
            if (file.Length > MaxBinaryUploadBytes)
            {
                ModelState.AddModelError(field.Name, $"{field.Label} must be {MaxBinaryUploadBytes / (1024 * 1024)} MB or smaller.");
                return;
            }

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream, cancellationToken);
            values[field.Name] = stream.ToArray();
            PostedValues[field.Name] = $"{stream.Length:N0} bytes";
            return;
        }

        if (!field.Required && form.ContainsKey(RemoveInputName(field)) && !IsNew && Entity is not null)
        {
            values[field.Name] = null;
            PostedValues[field.Name] = "removed";
            return;
        }

        if (IsNew && field.Required)
            ModelState.AddModelError(field.Name, $"{field.Label} is required.");
    }

    public string GetInputValue(DashboardField field)
    {
        if (PostedValues.TryGetValue(field.Name, out var posted)) return posted ?? string.Empty;
        if (Entity is null) return string.Empty;

        var value = EfDashboardRepository.GetValue(Entity, field);
        return value switch
        {
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("yyyy-MM-dd'T'HH:mm:ss.fff", CultureInfo.InvariantCulture),
            TimeOnly time => time.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
            _ => converter.Format(field, value)
        };
    }

    public string OffsetInputName(DashboardField field) => OffsetPrefix + field.Name;

    public string GetOffsetInputValue(DashboardField field)
    {
        if (PostedValues.TryGetValue(OffsetInputName(field), out var posted)) return posted ?? string.Empty;
        if (Entity is null) return "+00:00";
        return EfDashboardRepository.GetValue(Entity, field) is DateTimeOffset value
            ? value.ToString("zzz", CultureInfo.InvariantCulture)
            : "+00:00";
    }

    public string InputType(DashboardField field) => field switch
    {
        TextField when field.Editor == DashboardEditors.Email => "email",
        TextField when field.Editor == DashboardEditors.Url => "url",
        TextField when field.Editor == DashboardEditors.ImageUrl => "url",
        TextField when field.Editor == DashboardEditors.Telephone => "tel",
        NumberField => "number",
        DateTimeField when (Nullable.GetUnderlyingType(field.PropertyType) ?? field.PropertyType) == typeof(DateOnly) => "date",
        DateTimeField when (Nullable.GetUnderlyingType(field.PropertyType) ?? field.PropertyType) == typeof(TimeOnly) => "time",
        DateTimeField when (Nullable.GetUnderlyingType(field.PropertyType) ?? field.PropertyType) is var type &&
            (type == typeof(DateTime) || type == typeof(DateTimeOffset)) => "datetime-local",
        _ => "text"
    };

    public string? ImageUrlPreview(DashboardField field)
        => DashboardImages.GetSafeRemoteUrl(GetInputValue(field));

    public string? InputStep(DashboardField field)
    {
        var type = Nullable.GetUnderlyingType(field.PropertyType) ?? field.PropertyType;
        if (field is NumberField && (type == typeof(float) || type == typeof(double) || type == typeof(decimal))) return "any";
        return field is DateTimeField && (type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeOnly))
            ? "any"
            : null;
    }

    public bool IsDateTimeOffset(DashboardField field) =>
        field is DateTimeField && (Nullable.GetUnderlyingType(field.PropertyType) ?? field.PropertyType) == typeof(DateTimeOffset);

    public bool IsTimeSpan(DashboardField field) =>
        field is DateTimeField && (Nullable.GetUnderlyingType(field.PropertyType) ?? field.PropertyType) == typeof(TimeSpan);

    public static bool IsFlagsEnum(EnumField field) =>
        field.EnumType.IsDefined(typeof(FlagsAttribute), inherit: false);

    public static bool IsEnumOptionSelected(EnumField field, string value, string option)
    {
        if (!Enum.TryParse(field.EnumType, value, ignoreCase: true, out var selected) || selected is not Enum selectedEnum)
            return false;

        var optionEnum = (Enum)Enum.Parse(field.EnumType, option);
        var zero = (Enum)Enum.ToObject(field.EnumType, 0);
        return optionEnum.Equals(zero) ? selectedEnum.Equals(zero) : selectedEnum.HasFlag(optionEnum);
    }

    private async Task<bool> LoadAsync(string resourceName, string? id, CancellationToken cancellationToken)
    {
        Resource = resources.Find(resourceName);
        if (Resource is null) return false;
        Id = id;
        EditableFields = Resource.Fields.Where(field =>
            !field.Hidden && !field.HiddenInEditor && (!field.IsKey || IsNew && !field.ReadOnly))
            .OrderBy(field => Resource.EntityType.GetProperty(field.Name)?.MetadataToken ?? int.MaxValue)
            .ToArray();

        if (!string.IsNullOrWhiteSpace(id))
        {
            if (!converter.TryConvert(Resource.Key, id, out var key, out _) || key is null) return false;
            Key = key;
            Entity = await repository.FindAsync(Resource, key, cancellationToken);
            if (Entity is null) return false;
        }

        if (IsNew && !string.IsNullOrWhiteSpace(ParentField) && !string.IsNullOrWhiteSpace(ParentValue))
        {
            var relation = EditableFields.OfType<RelationField>().FirstOrDefault(field =>
                !field.ReadOnly && field.Name.Equals(ParentField, StringComparison.OrdinalIgnoreCase));
            if (relation is not null && converter.TryConvert(relation, ParentValue, out var value, out _) && value is not null)
            {
                ParentField = relation.Name;
                ParentValue = converter.Format(relation, value);
                PostedValues[relation.Name] = ParentValue;
            }
        }

        foreach (var relation in EditableFields.OfType<RelationField>())
        {
            var related = resources.Find(relation.RelatedEntityType);
            if (related is null)
            {
                Relationships[relation.Name] = [];
                continue;
            }

            RelatedLabels[relation.Name] = related.Label;
            var options = (await repository.LookupAsync(
                related,
                dependentResource: Resource,
                relation: relation,
                cancellationToken: cancellationToken)).ToList();
            var currentValue = GetInputValue(relation);
            if (currentValue.Length > 0 && options.All(option => option.Value != currentValue))
            {
                var current = await ResolveLookupOptionAsync(related, currentValue, cancellationToken);
                if (current is not null) options.Add(current);
            }
            Relationships[relation.Name] = options;
        }

        if (Entity is not null)
            RelatedRecords.AddRange(RelatedRecordLink.CreateMany(resources, converter, Resource, Entity));
        return true;
    }

    /// <summary>
    /// HTMX endpoint powering the relation comboboxes. Returns matching listbox options.
    /// </summary>
    public async Task<IActionResult> OnGetLookupAsync(
        string resourceName,
        string field,
        string? term,
        bool required = false,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var dependentResource = resources.Find(resourceName);
        var relation = dependentResource?.Fields.OfType<RelationField>().FirstOrDefault(candidate =>
            candidate.Name.Equals(field, StringComparison.OrdinalIgnoreCase));
        var relatedResource = relation is null ? null : resources.Find(relation.RelatedEntityType);
        if (dependentResource is null || relation is null || relatedResource is null) return NotFound();

        var currentValue = Request.Query.TryGetValue(field, out var currentValues)
            ? currentValues.ToString()
            : string.Empty;
        var options = (await repository.LookupAsync(
            relatedResource,
            search: term,
            take: take,
            dependentResource: dependentResource,
            relation: relation,
            cancellationToken: cancellationToken)).ToList();
        if (currentValue.Length > 0 && string.IsNullOrWhiteSpace(term) && options.All(option => option.Value != currentValue))
        {
            var current = await ResolveLookupOptionAsync(relatedResource, currentValue, cancellationToken);
            if (current is not null) options.Add(current);
        }

        var html = new StringBuilder();
        if (!required && string.IsNullOrWhiteSpace(term))
            AppendLookupOption(html, string.Empty, "No selection", currentValue.Length == 0);
        foreach (var option in options)
            AppendLookupOption(html, option.Value, option.Label, option.Value == currentValue);
        if (html.Length == 0)
            html.Append("""<div class="dash-lookup-empty">No matching records</div>""");
        return new ContentResult { Content = html.ToString(), ContentType = "text/html" };
    }

    private static void AppendLookupOption(StringBuilder html, string value, string label, bool selected)
    {
        var encodedValue = WebUtility.HtmlEncode(value);
        var encodedLabel = WebUtility.HtmlEncode(label);
        html.Append($"<button type=\"button\" class=\"dash-lookup-option\" role=\"option\" aria-selected=\"{selected.ToString().ToLowerInvariant()}\" data-value=\"{encodedValue}\" data-label=\"{encodedLabel}\"><span>{encodedLabel}</span><span class=\"dash-lookup-check\"></span></button>");
    }

    private async Task<DashboardLookupOption?> ResolveLookupOptionAsync(
        DashboardResource related,
        string currentValue,
        CancellationToken cancellationToken)
    {
        if (!converter.TryConvert(related.Key, currentValue, out var key, out _) || key is null) return null;
        var current = await repository.FindAsync(related, key, cancellationToken);
        if (current is null) return null;
        var displayField = related.DisplayField ?? related.Key;
        var label = converter.Format(displayField, EfDashboardRepository.GetValue(current, displayField));
        return new DashboardLookupOption(currentValue, label);
    }

    private async Task PreservePostedRelationshipsAsync(CancellationToken cancellationToken)
    {
        foreach (var relation in EditableFields.OfType<RelationField>())
        {
            if (!PostedValues.TryGetValue(relation.Name, out var value) || string.IsNullOrWhiteSpace(value)) continue;
            var options = Relationships[relation.Name].ToList();
            if (options.Any(option => option.Value == value)) continue;

            var related = resources.Find(relation.RelatedEntityType);
            if (related is null) continue;
            var option = await ResolveLookupOptionAsync(related, value, cancellationToken);
            if (option is not null) options.Add(option);
            Relationships[relation.Name] = options;
        }
    }
}
