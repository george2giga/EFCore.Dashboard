namespace EFCore.Dashboard.Authentication.Cookies;

/// <summary>Names and default routes used by the optional dashboard cookie authentication package.</summary>
public static class DashboardCookieAuthenticationDefaults
{
    public const string AuthenticationScheme = "EFCore.Dashboard.Cookies";
    public const string AuthorizationPolicy = "EFCore.Dashboard.CookieAdministrator";
    public const string RoutePrefix = "/efcore-dashboard/account";
    public const string LoginPath = "/efcore-dashboard/account/login";
    public const string LogoutPath = "/efcore-dashboard/account/logout";
    public const string AccessDeniedPath = "/efcore-dashboard/account/access-denied";
    public const string AccountPartial = "/Pages/Shared/_EFCoreDashboardCookieAccount.cshtml";
}
