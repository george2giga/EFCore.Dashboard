using Microsoft.EntityFrameworkCore.Metadata;
using EFCore.Dashboard.Core;

namespace EFCore.Dashboard.Extensibility;

/// <summary>Maps EF scalar-property metadata to dashboard field metadata.</summary>
public interface IDashboardFieldProvider
{
    /// <summary>
    /// Gets the selection priority. Lower values run first; the built-in fallback uses
    /// <see cref="int.MaxValue"/>.
    /// </summary>
    int Order => 0;
    /// <summary>Returns whether this provider can represent the mapped property.</summary>
    bool CanHandle(IProperty property);
    /// <summary>Creates field metadata using the EF-derived and consumer-configured context.</summary>
    DashboardField Create(IProperty property, DashboardFieldContext context);
}

/// <summary>Contains field settings already resolved from EF metadata and configuration overrides.</summary>
public sealed record DashboardFieldContext(
    string Label,
    bool Required,
    bool ReadOnly,
    bool Hidden,
    bool IsKey,
    int? MaxLength,
    string? Editor)
{
    /// <summary>Gets whether the field is omitted from lists, search, and exports.</summary>
    public bool HiddenInList { get; init; }
    /// <summary>Gets whether the field is omitted from create and edit forms.</summary>
    public bool HiddenInEditor { get; init; }
}
