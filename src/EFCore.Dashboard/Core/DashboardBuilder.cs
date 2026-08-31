using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using EFCore.Dashboard.Extensibility;

namespace EFCore.Dashboard.Core;

/// <summary>Configures resource conventions and extension points before dashboard metadata is built.</summary>
public sealed class DashboardBuilder
{
    private readonly IServiceCollection _services;
    internal DashboardOptions Options { get; }

    internal DashboardBuilder(IServiceCollection services, DashboardOptions options)
    {
        _services = services;
        Options = options;
    }

    /// <summary>Adds or updates configuration for a discovered EF entity type.</summary>
    public DashboardBuilder Resource<TEntity>(Action<DashboardResourceBuilder<TEntity>>? configure = null)
        where TEntity : class
    {
        var builder = new DashboardResourceBuilder<TEntity>(Options.GetOrCreate(typeof(TEntity)));
        configure?.Invoke(builder);
        return this;
    }

    /// <summary>Prevents an EF entity type from being exposed as a dashboard resource.</summary>
    public DashboardBuilder Exclude<TEntity>() where TEntity : class
    {
        Options.GetOrCreate(typeof(TEntity)).Excluded = true;
        return this;
    }

    /// <summary>Requires the host's named authorization policy for every dashboard page.</summary>
    public DashboardBuilder RequireAuthorization(string policyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        Options.AllowAnonymous = false;
        Options.AuthorizationPolicy = policyName;
        return this;
    }

    /// <summary>
    /// Sets the root route prefix for the dashboard pages. Leading and trailing slashes
    /// are optional; the default prefix is <c>/admin</c>. The prefix must be a literal
    /// path without route parameters, for example <c>"/data/dashboard"</c>.
    /// </summary>
    public DashboardBuilder UseRoutePrefix(string routePrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routePrefix);
        if (routePrefix.Contains('{') || routePrefix.Contains('}'))
            throw new ArgumentException("The dashboard route prefix cannot contain route parameters.", nameof(routePrefix));

        Options.RoutePrefix = "/" + routePrefix.Trim('/', ' ');
        return this;
    }

    /// <summary>
    /// Allows unauthenticated access to every dashboard page. Intended for samples and local development only.
    /// </summary>
    public DashboardBuilder AllowAnonymous()
    {
        Options.AllowAnonymous = true;
        Options.AuthorizationPolicy = null;
        return this;
    }

    /// <summary>Renders a host-provided Razor partial in the dashboard top bar.</summary>
    public DashboardBuilder AccountPartial(string partialViewName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partialViewName);
        Options.AccountPartial = partialViewName;
        return this;
    }

    /// <summary>Adds a host stylesheet after the built-in dashboard styles.</summary>
    public DashboardBuilder AddStylesheet(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Options.AddStylesheet(path);
        return this;
    }

    /// <summary>
    /// Registers a singleton field provider. Providers run by ascending
    /// <see cref="IDashboardFieldProvider.Order"/> before the built-in fallback.
    /// </summary>
    public DashboardBuilder AddFieldProvider<TProvider>()
        where TProvider : class, IDashboardFieldProvider
    {
        _services.AddSingleton<IDashboardFieldProvider, TProvider>();
        return this;
    }

}

/// <summary>Overrides conventions for one discovered entity type.</summary>
public sealed class DashboardResourceBuilder<TEntity> where TEntity : class
{
    private readonly ResourceOptions _options;

    internal DashboardResourceBuilder(ResourceOptions options) => _options = options;

    /// <summary>Sets the plural, human-readable resource label.</summary>
    public DashboardResourceBuilder<TEntity> Label(string label)
    {
        _options.Label = label;
        return this;
    }

    /// <summary>Selects the property used to label lookup options and as the default list sort.</summary>
    public DashboardResourceBuilder<TEntity> Display<TValue>(Expression<Func<TEntity, TValue>> expression)
    {
        _options.DisplayProperty = GetPropertyName(expression);
        return this;
    }

    /// <summary>Configures presentation and edit behavior for a mapped scalar property.</summary>
    public DashboardFieldBuilder Field<TValue>(Expression<Func<TEntity, TValue>> expression)
        => new(_options.GetOrCreateField(GetPropertyName(expression)));

    private static string GetPropertyName<TValue>(Expression<Func<TEntity, TValue>> expression)
    {
        var body = expression.Body is UnaryExpression unary ? unary.Operand : expression.Body;
        if (body is not MemberExpression { Member: PropertyInfo } member || member.Expression != expression.Parameters[0])
            throw new ArgumentException("Expression must select a property.", nameof(expression));
        return member.Member.Name;
    }
}

/// <summary>Overrides field metadata inferred from EF Core.</summary>
public sealed class DashboardFieldBuilder
{
    private readonly FieldOptions _options;
    internal DashboardFieldBuilder(FieldOptions options) => _options = options;

    /// <summary>Sets the human-readable field label.</summary>
    public DashboardFieldBuilder Label(string label) { _options.Label = label; return this; }
    /// <summary>Excludes the field from lists, search, exports, forms, and the data-model diagram.</summary>
    public DashboardFieldBuilder Hidden(bool hidden = true) { _options.Hidden = hidden; return this; }
    /// <summary>Excludes the field from lists, search, and exports while keeping it in forms.</summary>
    public DashboardFieldBuilder HiddenInList(bool hidden = true) { _options.HiddenInList = hidden; return this; }
    /// <summary>Excludes the field from create and edit forms while keeping it in lists.</summary>
    public DashboardFieldBuilder HiddenInEditor(bool hidden = true) { _options.HiddenInEditor = hidden; return this; }
    /// <summary>Prevents submitted values from being assigned while keeping the field visible.</summary>
    public DashboardFieldBuilder ReadOnly(bool readOnly = true) { _options.ReadOnly = readOnly; return this; }
    /// <summary>Sets a supported <see cref="DashboardEditors"/> rendering hint for the built-in form.</summary>
    public DashboardFieldBuilder Editor(string editor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(editor);
        _options.Editor = DashboardEditors.Normalize(editor);
        return this;
    }
}
