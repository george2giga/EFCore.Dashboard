using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using EFCore.Dashboard.Core;
using EFCore.Dashboard.EntityFrameworkCore;
using EFCore.Dashboard.Extensibility;
using EFCore.Dashboard.Fields;
using EFCore.Dashboard.Web;

namespace EFCore.Dashboard;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the dashboard Razor Pages and default services over the host's existing EF Core context.
    /// Dashboard pages require the host's default authorization policy unless explicitly configured otherwise.
    /// The host retains responsibility for database configuration, authentication, and policy definitions.
    /// </summary>
    /// <typeparam name="TDbContext">The context already registered and configured by the host.</typeparam>
    /// <param name="services">The host service collection.</param>
    /// <param name="configure">Optional convention overrides and extensions, applied before metadata is discovered.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddEFCoreDashboard<TDbContext>(
        this IServiceCollection services,
        Action<DashboardBuilder>? configure = null)
        where TDbContext : DbContext
    {
        var options = new DashboardOptions { DbContextType = typeof(TDbContext) };
        services.AddRazorPages(razorPages =>
        {
            ApplyDashboardRoutes(razorPages.Conventions, options.RoutePrefix);
            if (options.AllowAnonymous)
                razorPages.Conventions.AllowAnonymousToFolder("/Admin");
            else if (options.AuthorizationPolicy is { } policy)
                razorPages.Conventions.AuthorizeFolder("/Admin", policy);
            else
                razorPages.Conventions.AuthorizeFolder("/Admin");
        }).AddApplicationPart(typeof(ServiceCollectionExtensions).Assembly);

        services.AddSingleton(options);
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<TDbContext>());
        services.AddScoped<IDashboardRepository, EfDashboardRepository>();
        services.AddSingleton<IDashboardResourceProvider, EfResourceProvider>();
        services.AddSingleton<IDashboardValueConverter, DashboardValueConverter>();
        services.AddSingleton<IDashboardFieldProvider, DefaultFieldProvider>();
        services.AddSingleton<DashboardDiagramRenderer>();

        configure?.Invoke(new DashboardBuilder(services, options));
        return services;
    }

    private static void ApplyDashboardRoutes(PageConventionCollection conventions, string prefix)
    {
        conventions.AddDashboardRoute("/Admin/Index", prefix);
        conventions.AddDashboardRoute("/Admin/Schema", $"{prefix}/data-model");
        conventions.AddDashboardRoute("/Admin/Resource", $"{prefix}/{{resourceName}}");
        conventions.AddDashboardRoute("/Admin/Edit", $"{prefix}/{{resourceName}}/edit/{{id?}}");
    }
}

internal static class DashboardRouteConventions
{
    /// <summary>
    /// Replaces a page's routes with a single configured template so the dashboard only
    /// responds where the host placed it.
    /// </summary>
    public static void AddDashboardRoute(this PageConventionCollection conventions, string pageName, string route)
    {
        conventions.AddPageRouteModelConvention(pageName, model =>
        {
            model.Selectors.Clear();
            model.Selectors.Add(new SelectorModel
            {
                AttributeRouteModel = new AttributeRouteModel { Template = route }
            });
        });
    }
}
