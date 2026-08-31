namespace EFCore.Dashboard.Core;

/// <summary>Describes one discovered, manageable EF entity type.</summary>
public sealed record DashboardResource
{
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public required string Label { get; init; }
    public required Type EntityType { get; init; }
    /// <summary>Gets the single-column primary-key field used for route and repository lookup.</summary>
    public required DashboardField Key { get; init; }
    public required IReadOnlyList<DashboardField> Fields { get; init; }
    /// <summary>Gets the field used for lookup labels and default list ordering.</summary>
    public DashboardField? DisplayField { get; init; }
    /// <summary>
    /// Gets outgoing relationships usable by the delete guard. The default provider includes
    /// only discovered dependents with a single-column foreign key in this per-resource list.
    /// </summary>
    public IReadOnlyList<DashboardRelationship>? Relationships { get; init; }
}
