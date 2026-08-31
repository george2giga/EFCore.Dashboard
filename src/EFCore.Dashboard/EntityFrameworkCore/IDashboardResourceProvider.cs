using EFCore.Dashboard.Core;

namespace EFCore.Dashboard.EntityFrameworkCore;

/// <summary>Exposes the resource and relationship metadata used by dashboard pages and repositories.</summary>
public interface IDashboardResourceProvider
{
    /// <summary>Gets discovered resources; the default provider builds and caches them on first access.</summary>
    IReadOnlyList<DashboardResource> GetResources();
    /// <summary>Gets relationships whose endpoints are both discovered resources.</summary>
    IReadOnlyList<DashboardRelationship> GetRelationships();
    /// <summary>Finds a resource by CLR type name or generated slug, ignoring case.</summary>
    DashboardResource? Find(string nameOrSlug);
    /// <summary>Finds the resource for an exact CLR entity type.</summary>
    DashboardResource? Find(Type entityType);
}
