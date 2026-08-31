using EFCore.Dashboard.Authentication.Cookies;
using EFCore.Dashboard.Core;

namespace EFCore.Dashboard;

/// <summary>Connects the dashboard pages and account UI to the optional cookie authentication package.</summary>
public static class DashboardBuilderExtensions
{
    public static DashboardBuilder UseCookieAuthentication(this DashboardBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder
            .RequireAuthorization(DashboardCookieAuthenticationDefaults.AuthorizationPolicy)
            .AccountPartial(DashboardCookieAuthenticationDefaults.AccountPartial);
    }
}
