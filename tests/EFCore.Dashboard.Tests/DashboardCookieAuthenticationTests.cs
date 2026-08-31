using System.Net;
using System.Text.RegularExpressions;
using EFCore.Dashboard.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace EFCore.Dashboard.Tests;

public sealed partial class DashboardCookieAuthenticationTests
{
    private const string Username = "dashboard-admin";
    private const string Password = "correct-horse-battery-staple";

    [Fact]
    public async Task AnonymousDashboardRequestRedirectsToPackagedLoginPage()
    {
        await using var app = await BuildAppAsync();

        var response = await app.GetTestClient().GetAsync("/admin");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            "/efcore-dashboard/account/login?returnUrl=%2Fadmin",
            response.Headers.Location?.PathAndQuery);
    }

    [Fact]
    public async Task LoginLoadsCoreAuthenticationAndHostStylesInOrder()
    {
        await using var app = await BuildAppAsync();

        var response = await app.GetTestClient().GetAsync(DashboardCookieAuthenticationDefaults.LoginPath);
        var content = await response.Content.ReadAsStringAsync();
        var coreIndex = content.IndexOf("/_content/EFCore.Dashboard/dashboard.css", StringComparison.Ordinal);
        var authenticationIndex = content.IndexOf(
            "/_content/EFCore.Dashboard.Authentication.Cookies/authentication.css",
            StringComparison.Ordinal);
        var hostIndex = content.IndexOf("/custom-dashboard.css", StringComparison.Ordinal);

        Assert.True(coreIndex >= 0);
        Assert.True(authenticationIndex > coreIndex);
        Assert.True(hostIndex > authenticationIndex);
        Assert.Contains("dash-panel efcd-auth-card", content);
        Assert.Contains("class=\"dash-input\"", content);
        Assert.Contains("dash-btn dash-btn-primary dash-btn-block", content);
    }

    [Fact]
    public async Task ValidCredentialsCreateCookieAndRenderDashboardAccountUi()
    {
        await using var app = await BuildAppAsync();
        var client = app.GetTestClient();
        var login = await GetLoginFormAsync(client);

        var response = await PostLoginAsync(client, login, Username, Password, "/admin");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/admin", response.Headers.Location?.OriginalString);
        var authenticationCookie = GetCookie(response, ".EFCore.Dashboard.Authentication");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/admin");
        request.Headers.Add("Cookie", authenticationCookie);
        var dashboard = await client.SendAsync(request);
        var content = await dashboard.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
        Assert.Contains(Username, content);
        Assert.Contains("Sign out", content);
        Assert.Contains("/custom-dashboard.css", content);
    }

    [Fact]
    public async Task ValidLogoutDeletesAuthenticationCookie()
    {
        await using var app = await BuildAppAsync();
        var client = app.GetTestClient();
        var login = await GetLoginFormAsync(client);
        var signIn = await PostLoginAsync(client, login, Username, Password, "/admin");
        var authenticationCookie = GetCookie(signIn, ".EFCore.Dashboard.Authentication");

        using var dashboardRequest = new HttpRequestMessage(HttpMethod.Get, "/admin");
        dashboardRequest.Headers.Add("Cookie", $"{login.Cookie}; {authenticationCookie}");
        var dashboard = await client.SendAsync(dashboardRequest);
        var dashboardContent = await dashboard.Content.ReadAsStringAsync();
        var logoutToken = AntiforgeryTokenRegex().Match(dashboardContent).Groups[1].Value;

        using var logoutRequest = new HttpRequestMessage(
            HttpMethod.Post,
            DashboardCookieAuthenticationDefaults.LogoutPath);
        logoutRequest.Headers.Add("Cookie", $"{login.Cookie}; {authenticationCookie}");
        logoutRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = logoutToken
        });
        var logout = await client.SendAsync(logoutRequest);

        Assert.NotEmpty(logoutToken);
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Contains(GetSetCookies(logout), value =>
            value.StartsWith(".EFCore.Dashboard.Authentication=", StringComparison.Ordinal)
            && value.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task LogoutWithoutAntiforgeryTokenIsRejected()
    {
        await using var app = await BuildAppAsync();
        var client = app.GetTestClient();
        var login = await GetLoginFormAsync(client);
        var signIn = await PostLoginAsync(client, login, Username, Password, "/admin");
        var authenticationCookie = GetCookie(signIn, ".EFCore.Dashboard.Authentication");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            DashboardCookieAuthenticationDefaults.LogoutPath);
        request.Headers.Add("Cookie", authenticationCookie);
        request.Content = new FormUrlEncodedContent([]);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProductionCookieHasSecureBrowserProtections()
    {
        await using var app = await BuildAppAsync(allowInsecureHttp: false);
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://localhost");
        var login = await GetLoginFormAsync(client);

        var response = await PostLoginAsync(client, login, Username, Password, "/admin");
        var cookie = GetSetCookies(response).Single(value =>
            value.StartsWith(".EFCore.Dashboard.Authentication=", StringComparison.Ordinal));

        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExternalReturnUrlIsNotUsedAfterLogin()
    {
        await using var app = await BuildAppAsync();
        var client = app.GetTestClient();
        var login = await GetLoginFormAsync(client);

        var response = await PostLoginAsync(client, login, Username, Password, "https://example.com/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/admin", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task InvalidCredentialsReturnGenericErrorWithoutCookie()
    {
        await using var app = await BuildAppAsync();
        var client = app.GetTestClient();
        var login = await GetLoginFormAsync(client);

        var response = await PostLoginAsync(client, login, Username, "wrong-password", "/admin");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Sign-in failed", content);
        Assert.DoesNotContain(GetSetCookies(response), value =>
            value.StartsWith(".EFCore.Dashboard.Authentication=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CorrectCredentialsCanRecoverFromClientLockout()
    {
        await using var app = await BuildAppAsync(maxFailedAttempts: 2);
        var client = app.GetTestClient();
        var login = await GetLoginFormAsync(client);

        await PostLoginAsync(client, login, Username, "wrong-password", "/admin");
        await PostLoginAsync(client, login, Username, "wrong-password", "/admin");
        var response = await PostLoginAsync(client, login, Username, Password, "/admin");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(GetSetCookies(response), value =>
            value.StartsWith(".EFCore.Dashboard.Authentication=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WrongCredentialsRemainThrottledAfterAttemptLimit()
    {
        await using var app = await BuildAppAsync(maxFailedAttempts: 2);
        var client = app.GetTestClient();
        var login = await GetLoginFormAsync(client);

        await PostLoginAsync(client, login, Username, "wrong-password", "/admin");
        await PostLoginAsync(client, login, Username, "wrong-password", "/admin");
        var response = await PostLoginAsync(client, login, Username, "another-wrong-password", "/admin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Retry-After", out var values));
        Assert.Equal("300", values.Single());
    }

    [Fact]
    public async Task LoginRejectsHttpByDefault()
    {
        await using var app = await BuildAppAsync(allowInsecureHttp: false);

        var response = await app.GetTestClient().GetAsync(DashboardCookieAuthenticationDefaults.LoginPath);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("EFCore.Dashboard cookie authentication requires HTTPS.",
            await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public void RegistrationRejectsWeakPassword()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddEFCoreDashboardCookieAuthentication(options =>
            {
                options.Username = Username;
                options.Password = "too-short";
            }));

        Assert.Contains("at least 12 characters", exception.Message);
    }

    [Fact]
    public void RegistrationDoesNotReplaceHostDefaultScheme()
    {
        var services = new ServiceCollection();
        services.AddAuthentication("HostScheme");
        services.AddEFCoreDashboardCookieAuthentication(options =>
        {
            options.Username = Username;
            options.Password = Password;
        });

        using var provider = services.BuildServiceProvider();
        var authentication = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;

        Assert.Equal("HostScheme", authentication.DefaultScheme);
    }

    [Fact]
    public async Task CustomAccountRoutePrefixMovesAccountPagesAndRedirectHome()
    {
        await using var app = await BuildAppAsync(cookieConfigure: options =>
        {
            options.RoutePrefix = "/secure/sign-in";
        });
        var client = app.GetTestClient();

        var login = await client.GetAsync("/secure/sign-in/login");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(DashboardCookieAuthenticationDefaults.LoginPath)).StatusCode);

        var loginForm = await GetLoginFormAsync(client, "/secure/sign-in/login");
        var response = await PostLoginAsync(client, loginForm, Username, Password, "", "/secure/sign-in/login");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/admin", response.Headers.Location?.OriginalString);
    }

    private static async Task<WebApplication> BuildAppAsync(
        bool allowInsecureHttp = true,
        int maxFailedAttempts = 5,
        Action<DashboardCookieAuthenticationOptions>? cookieConfigure = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddDbContext<CookieAuthenticationTestContext>(options =>
            options.UseInMemoryDatabase($"dashboard-cookie-authentication-{Guid.NewGuid()}"));
        builder.Services.AddEFCoreDashboardCookieAuthentication(options =>
        {
            options.Username = Username;
            options.Password = Password;
            options.AllowInsecureHttp = allowInsecureHttp;
            options.MaxFailedAttempts = maxFailedAttempts;
            cookieConfigure?.Invoke(options);
        });
        builder.Services.AddEFCoreDashboard<CookieAuthenticationTestContext>(dashboard =>
            dashboard.UseCookieAuthentication().AddStylesheet("~/custom-dashboard.css"));

        var app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapRazorPages();
        await app.StartAsync();
        return app;
    }

    private static async Task<LoginForm> GetLoginFormAsync(HttpClient client, string? path = null)
    {
        var response = await client.GetAsync(path ?? DashboardCookieAuthenticationDefaults.LoginPath);
        var content = await response.Content.ReadAsStringAsync();
        var token = AntiforgeryTokenRegex().Match(content).Groups[1].Value;
        var cookie = GetCookie(response, ".AspNetCore.Antiforgery");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty(token);
        return new LoginForm(token, cookie);
    }

    private static async Task<HttpResponseMessage> PostLoginAsync(
        HttpClient client,
        LoginForm login,
        string username,
        string password,
        string returnUrl,
        string? path = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path ?? DashboardCookieAuthenticationDefaults.LoginPath);
        request.Headers.Add("Cookie", login.Cookie);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Username"] = username,
            ["Password"] = password,
            ["ReturnUrl"] = returnUrl,
            ["__RequestVerificationToken"] = login.Token
        });
        return await client.SendAsync(request);
    }

    private static string GetCookie(HttpResponseMessage response, string namePrefix)
    {
        var setCookie = GetSetCookies(response)
            .Single(value => value.StartsWith(namePrefix, StringComparison.Ordinal));
        return setCookie[..setCookie.IndexOf(';')];
    }

    private static IEnumerable<string> GetSetCookies(HttpResponseMessage response)
        => response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryTokenRegex();

    private sealed record LoginForm(string Token, string Cookie);

    private sealed class CookieAuthenticationTestContext(DbContextOptions<CookieAuthenticationTestContext> options)
        : DbContext(options)
    {
        public DbSet<CookieAuthenticationTestEntity> Entities => Set<CookieAuthenticationTestEntity>();
    }

    private sealed class CookieAuthenticationTestEntity
    {
        public int Id { get; set; }
    }
}
