namespace EFCore.Dashboard.Core;

/// <summary>Describes one mapped scalar property independently of an entity instance.</summary>
public abstract record DashboardField
{
    public required string Name { get; init; }
    public required string Label { get; init; }
    public required Type PropertyType { get; init; }
    public bool Required { get; init; }
    /// <summary>Gets whether submitted values must be ignored by the repository.</summary>
    public bool ReadOnly { get; init; }
    /// <summary>Gets whether the field is omitted from every built-in surface.</summary>
    public bool Hidden { get; init; }
    /// <summary>Gets whether the field is omitted from built-in lists, search, and exports.</summary>
    public bool HiddenInList { get; init; }
    /// <summary>Gets whether the field is omitted from built-in create and edit forms.</summary>
    public bool HiddenInEditor { get; init; }
    /// <summary>Gets whether the field is the resource's single-column primary key.</summary>
    public bool IsKey { get; init; }
    public int? MaxLength { get; init; }
    public string? Editor { get; init; }
}

public sealed record TextField : DashboardField;
public sealed record NumberField : DashboardField;
public sealed record BooleanField : DashboardField;
public sealed record DateTimeField : DashboardField;

/// <summary>
/// Describes a <c>byte[]</c> property opt-in as a binary image upload. The host chooses
/// this with <c>.Editor(DashboardEditors.Image)</c>; byte[] properties are otherwise not exposed.
/// </summary>
public sealed record BinaryField : DashboardField;

/// <summary>Describes an enum property and the enum type used to populate its editor.</summary>
public sealed record EnumField : DashboardField
{
    public required Type EnumType { get; init; }
}

/// <summary>Describes a scalar foreign-key property whose principal is another discovered resource.</summary>
public sealed record RelationField : DashboardField
{
    public required Type RelatedEntityType { get; init; }
    public required string RelatedResourceName { get; init; }
    /// <summary>Gets the single principal-key property referenced by this foreign key.</summary>
    public required string PrincipalKeyProperty { get; init; }
    /// <summary>Gets whether each principal can be referenced by at most one dependent.</summary>
    public bool IsUnique { get; init; }
}
