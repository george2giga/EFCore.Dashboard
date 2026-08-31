using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using EFCore.Dashboard.Core;
using EFCore.Dashboard.EntityFrameworkCore;

namespace EFCore.Dashboard.Pages.Admin;

[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class IndexModel(
    IDashboardResourceProvider resources,
    IDashboardRepository repository,
    DashboardOptions options) : PageModel
{
    public IReadOnlyList<ResourceCount> Resources { get; private set; } = [];
    public string DbContextName => options.DbContextType?.Name
        ?? throw new InvalidOperationException("The dashboard DbContext type was not configured.");

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var list = new List<ResourceCount>();
        foreach (var resource in resources.GetResources())
            list.Add(new ResourceCount(resource, await repository.CountAsync(resource, cancellationToken)));
        Resources = list;
    }

    public sealed record ResourceCount(DashboardResource Resource, int Count);
}
