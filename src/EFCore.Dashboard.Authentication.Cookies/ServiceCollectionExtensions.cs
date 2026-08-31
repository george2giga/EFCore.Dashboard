using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EFCore.Dashboard.Authentication.Cookies;

/// <summary>Registers the optional single-administrator cookie authentication flow.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEFCoreDashboardCookieAuthentication(
        this IServiceCollection services,
        Action<DashboardCookieAuthenticationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new DashboardCookieAuthenticationOptions();
        configure(options);
        var settings = options.Validate();

        services.AddSingleton(settings);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<DashboardCredentialValidator>();

        services
            .AddAuthentication()
            .AddCookie(DashboardCookieAuthenticationDefaults.AuthenticationScheme, cookie =>
            {
                cookie.Cookie.Name = settings.CookieName;
                cookie.Cookie.HttpOnly = true;
                cookie.Cookie.IsEssential = true;
                cookie.Cookie.SameSite = SameSiteMode.Lax;
                cookie.Cookie.SecurePolicy = settings.AllowInsecureHttp
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
                cookie.ExpireTimeSpan = settings.Expiration;
                cookie.SlidingExpiration = settings.SlidingExpiration;
                cookie.LoginPath = settings.LoginPath;
                cookie.LogoutPath = settings.LogoutPath;
                cookie.AccessDeniedPath = settings.AccessDeniedPath;
                cookie.ReturnUrlParameter = "returnUrl";
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(DashboardCookieAuthenticationDefaults.AuthorizationPolicy, policy =>
            {
                policy.AddAuthenticationSchemes(DashboardCookieAuthenticationDefaults.AuthenticationScheme);
                policy.RequireAuthenticatedUser();
            });

        services.AddRazorPages(razorPages =>
        {
            ApplyDashboardAuthRoutes(razorPages.Conventions, settings.RoutePrefix);
        }).AddApplicationPart(typeof(ServiceCollectionExtensions).Assembly);
        return services;
    }

    private static void ApplyDashboardAuthRoutes(PageConventionCollection conventions, string prefix)
    {
        conventions.AddDashboardRoute("/DashboardAuth/Login", $"{prefix}/login");
        conventions.AddDashboardRoute("/DashboardAuth/Logout", $"{prefix}/logout");
        conventions.AddDashboardRoute("/DashboardAuth/AccessDenied", $"{prefix}/access-denied");
    }
}

internal static class DashboardAuthRouteConventions
{
    /// <summary>
    /// Replaces a page's routes with a single configured template so the account pages
    /// only respond where the host placed them.
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
