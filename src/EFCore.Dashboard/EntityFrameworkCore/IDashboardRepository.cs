using EFCore.Dashboard.Core;

namespace EFCore.Dashboard.EntityFrameworkCore;

/// <summary>Defines metadata-driven persistence operations used by the dashboard UI.</summary>
public interface IDashboardRepository
{
    /// <summary>Returns one filtered and sorted page; the default implementation clamps page size to 1 through 100.</summary>
    Task<DashboardPage> QueryAsync(DashboardResource resource, DashboardQuery query, CancellationToken cancellationToken = default);
    /// <summary>Returns a filtered and sorted export, limited by <paramref name="take"/>.</summary>
    Task<IReadOnlyList<object>> ExportAsync(DashboardResource resource, DashboardQuery query, int take = 1000, CancellationToken cancellationToken = default);
    Task<object?> FindAsync(DashboardResource resource, object key, CancellationToken cancellationToken = default);
    /// <summary>Creates an entity and assigns writable fields, including a manually generated key.</summary>
    Task<object> CreateAsync(DashboardResource resource, IReadOnlyDictionary<string, object?> values, CancellationToken cancellationToken = default);
    /// <summary>Updates writable non-key fields; submitted key values are ignored.</summary>
    Task UpdateAsync(DashboardResource resource, object key, IReadOnlyDictionary<string, object?> values, CancellationToken cancellationToken = default);
    Task DeleteAsync(DashboardResource resource, object key, CancellationToken cancellationToken = default);
    /// <summary>Deletes the records matching <paramref name="keys"/> in one save operation.</summary>
    Task DeleteManyAsync(DashboardResource resource, IReadOnlyCollection<object> keys, CancellationToken cancellationToken = default);
    Task<int> CountAsync(DashboardResource resource, CancellationToken cancellationToken = default);
    /// <summary>
    /// Counts references that prevent deletion, excluding database cascade and set-null relationships.
    /// </summary>
    Task<int> CountChildrenAsync(DashboardResource resource, object key, CancellationToken cancellationToken = default);
    /// <summary>Returns dependent entity types, foreign-key fields, and counts that prevent deletion.</summary>
    Task<IReadOnlyList<DashboardDeleteReference>> GetDeleteBlockersAsync(DashboardResource resource, object key, CancellationToken cancellationToken = default);
    /// <summary>Returns display-sorted relationship options, optionally filtered by search and one-to-one availability.</summary>
    Task<IReadOnlyList<DashboardLookupOption>> LookupAsync(
        DashboardResource resource,
        string? search = null,
        int take = 250,
        DashboardResource? dependentResource = null,
        RelationField? relation = null,
        CancellationToken cancellationToken = default);
    /// <summary>Gets a related principal's resolved display value for a materialized list or export item.</summary>
    bool TryGetRelatedValue(object item, RelationField field, out object? value)
    {
        value = null;
        return false;
    }
}
