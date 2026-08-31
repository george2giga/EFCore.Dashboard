namespace EFCore.Dashboard.Core;

/// <summary>Identifies the cardinality represented by discovered EF relationship metadata.</summary>
public enum RelationshipMultiplicity
{
    OneToOne,
    OneToMany,
    ManyToMany
}

/// <summary>Describes how the database handles dependents when their principal is deleted.</summary>
public enum RelationshipDeleteBehavior
{
    Restrict,
    Cascade,
    SetNull
}

/// <summary>Reports dependent records that prevent a principal from being deleted.</summary>
public sealed record DashboardDeleteReference(Type EntityType, string ForeignKeyProperty, int Count);

/// <summary>Describes an EF relationship whose endpoints are discovered dashboard resources.</summary>
public sealed record DashboardRelationship
{
    public required Type SourceEntityType { get; init; }
    public required Type TargetEntityType { get; init; }
    public required RelationshipMultiplicity Multiplicity { get; init; }
    public required bool Required { get; init; }
    public RelationshipDeleteBehavior DeleteBehavior { get; init; }
    public string? NavigationName { get; init; }
    public string? InverseNavigationName { get; init; }
    /// <summary>
    /// Gets the dependent scalar property for a single-column foreign key, or <see langword="null"/>
    /// for composite and many-to-many relationships that cannot participate in the delete guard.
    /// </summary>
    public string? ForeignKeyProperty { get; init; }
    public string? JoinEntityTypeName { get; init; }
}
