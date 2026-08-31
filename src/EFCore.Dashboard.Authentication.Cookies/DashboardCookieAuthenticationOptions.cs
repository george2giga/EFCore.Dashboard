namespace EFCore.Dashboard.Authentication.Cookies;

/// <summary>Configures the single administrator credential and cookie session.</summary>
public sealed class DashboardCookieAuthenticationOptions
{
    /// <summary>The administrator username. Read it from configuration rather than hard-coding it.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>The administrator password. Supply it through user secrets or a secret provider.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Root route prefix for the login, logout, and access-denied pages.
    /// Defaults to <c>/efcore-dashboard/account</c>. Must be a literal path without route parameters.
    /// </summary>
    public string RoutePrefix { get; set; } = DashboardCookieAuthenticationDefaults.RoutePrefix;

    /// <summary>The authentication cookie name.</summary>
    public string CookieName { get; set; } = ".EFCore.Dashboard.Authentication";

    /// <summary>The idle authentication lifetime. Defaults to eight hours.</summary>
    public TimeSpan Expiration { get; set; } = TimeSpan.FromHours(8);

    /// <summary>Whether activity renews the cookie lifetime. Defaults to true.</summary>
    public bool SlidingExpiration { get; set; } = true;

    /// <summary>Failed attempts from one client before login is temporarily blocked.</summary>
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>How long a client is blocked after too many failed attempts.</summary>
    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Allows credentials and cookies over HTTP. Enable only for local development because HTTP exposes the password.
    /// </summary>
    public bool AllowInsecureHttp { get; set; }

    internal DashboardCookieAuthenticationSettings Validate()
    {
        if (string.IsNullOrWhiteSpace(Username))
            throw new InvalidOperationException("Dashboard cookie authentication requires a username.");
        if (Username.Length > 256)
            throw new InvalidOperationException("Dashboard cookie authentication usernames cannot exceed 256 characters.");
        if (string.IsNullOrEmpty(Password) || Password.Length < 12)
            throw new InvalidOperationException("Dashboard cookie authentication requires a password of at least 12 characters.");
        if (Password.Length > 1024)
            throw new InvalidOperationException("Dashboard cookie authentication passwords cannot exceed 1024 characters.");
        if (string.IsNullOrWhiteSpace(CookieName))
            throw new InvalidOperationException("Dashboard cookie authentication requires a cookie name.");
        if (string.IsNullOrWhiteSpace(RoutePrefix) || RoutePrefix.Contains('{') || RoutePrefix.Contains('}'))
            throw new InvalidOperationException("Dashboard cookie authentication requires a literal route prefix.");
        if (Expiration < TimeSpan.FromMinutes(5) || Expiration > TimeSpan.FromDays(30))
            throw new InvalidOperationException("Dashboard cookie expiration must be between 5 minutes and 30 days.");
        if (MaxFailedAttempts is < 1 or > 100)
            throw new InvalidOperationException("Dashboard maximum failed attempts must be between 1 and 100.");
        if (LockoutDuration < TimeSpan.FromSeconds(10) || LockoutDuration > TimeSpan.FromDays(1))
            throw new InvalidOperationException("Dashboard lockout duration must be between 10 seconds and 1 day.");

        var routePrefix = "/" + RoutePrefix.Trim('/', ' ');
        return new DashboardCookieAuthenticationSettings(
            Username,
            Password,
            CookieName,
            Expiration,
            SlidingExpiration,
            MaxFailedAttempts,
            LockoutDuration,
            AllowInsecureHttp,
            routePrefix,
            routePrefix + "/login",
            routePrefix + "/logout",
            routePrefix + "/access-denied");
    }
}

internal sealed record DashboardCookieAuthenticationSettings(
    string Username,
    string Password,
    string CookieName,
    TimeSpan Expiration,
    bool SlidingExpiration,
    int MaxFailedAttempts,
    TimeSpan LockoutDuration,
    bool AllowInsecureHttp,
    string RoutePrefix,
    string LoginPath,
    string LogoutPath,
    string AccessDeniedPath);
