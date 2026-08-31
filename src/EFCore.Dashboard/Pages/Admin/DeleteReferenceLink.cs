using EFCore.Dashboard.Core;
using EFCore.Dashboard.EntityFrameworkCore;

namespace EFCore.Dashboard.Pages.Admin;

public sealed record DeleteReferenceLink(
    string PrincipalLabel,
    string ResourceSlug,
    string ResourceLabel,
    string ForeignKeyProperty,
    string ForeignKeyLabel,
    string ForeignKeyValue,
    int Count)
{
    public static (IReadOnlyList<DeleteReferenceLink> Links, bool Complete) CreateMany(
        IDashboardResourceProvider resources,
        string principalLabel,
        string foreignKeyValue,
        IReadOnlyList<DashboardDeleteReference> references)
    {
        var links = new List<DeleteReferenceLink>(references.Count);
        foreach (var reference in references)
        {
            var resource = resources.Find(reference.EntityType);
            var foreignKey = resource?.Fields.FirstOrDefault(x => x.Name == reference.ForeignKeyProperty);
            if (resource is null || foreignKey is null) continue;
            links.Add(new DeleteReferenceLink(
                principalLabel,
                resource.Slug,
                resource.Label,
                foreignKey.Name,
                foreignKey.Label,
                foreignKeyValue,
                reference.Count));
        }
        return (links, links.Count == references.Count);
    }
}
