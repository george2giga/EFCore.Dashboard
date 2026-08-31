namespace EFCore.Dashboard.Core;

/// <summary>Stores configuration accumulated by <see cref="DashboardBuilder"/> during registration.</summary>
public sealed class DashboardOptions
{
    private readonly List<string> _additionalStylesheets = [];

    internal Dictionary<Type, ResourceOptions> Resources { get; } = [];
    internal bool AllowAnonymous { get; set; }
    internal string? AuthorizationPolicy { get; set; }
    internal string? AccountPartial { get; set; }
    internal Type? DbContextType { get; set; }

    /// <summary>Root route prefix for the dashboard pages. Defaults to <c>/admin</c>.</summary>
    internal string RoutePrefix { get; set; } = "/admin";

    /// <summary>Host stylesheets rendered after the built-in dashboard styles.</summary>
    public IReadOnlyList<string> AdditionalStylesheets => _additionalStylesheets;

    internal void AddStylesheet(string path) => _additionalStylesheets.Add(path);

    internal ResourceOptions GetOrCreate(Type type)
    {
        if (!Resources.TryGetValue(type, out var options))
        {
            options = new ResourceOptions();
            Resources[type] = options;
        }

        return options;
    }
}

internal sealed class ResourceOptions
{
    public string? Label { get; set; }
    public string? DisplayProperty { get; set; }
    public bool Excluded { get; set; }
    public Dictionary<string, FieldOptions> Fields { get; } = new(StringComparer.OrdinalIgnoreCase);

    public FieldOptions GetOrCreateField(string name)
    {
        if (!Fields.TryGetValue(name, out var field))
        {
            field = new FieldOptions();
            Fields[name] = field;
        }

        return field;
    }
}

internal sealed class FieldOptions
{
    public string? Label { get; set; }
    public bool? Hidden { get; set; }
    public bool? HiddenInList { get; set; }
    public bool? HiddenInEditor { get; set; }
    public bool? ReadOnly { get; set; }
    public string? Editor { get; set; }
}
