using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using EFCore.Dashboard.EntityFrameworkCore;
using EFCore.Dashboard.Web;

namespace EFCore.Dashboard.Pages.Admin;

[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class SchemaModel(IDashboardResourceProvider resources, DashboardDiagramRenderer diagramRenderer) : PageModel
{
    public string DashboardDiagramSvg { get; private set; } = string.Empty;
    public int EntityCount { get; private set; }
    public int RelationshipCount { get; private set; }

    public void OnGet()
    {
        var dashboardResources = resources.GetResources();
        var dashboardRelationships = resources.GetRelationships();
        DashboardDiagramSvg = diagramRenderer.Render(
            dashboardResources,
            dashboardRelationships,
            slug => Url.Page("/Admin/Resource", new { resourceName = slug }) ?? "/");
        EntityCount = dashboardResources.Count;
        RelationshipCount = dashboardRelationships.Count;
    }
}
