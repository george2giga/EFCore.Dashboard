using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using EFCore.Dashboard.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace EFCore.Dashboard.Tests;

public sealed class DashboardAuthorizationTests
{
    [Fact]
    public async Task DefaultPolicyRejectsAnonymousRequests()
    {
        await using var app = await BuildAppAsync(authenticated: false);

        var response = await app.GetTestClient().GetAsync("/admin");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DefaultPolicyAllowsAuthenticatedRequests()
    {
        await using var app = await BuildAppAsync(authenticated: true);

        var response = await app.GetTestClient().GetAsync("/admin");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        Assert.Contains("<code>AuthorizationTestContext</code>", html);
    }

    [Fact]
    public async Task NamedPolicyForbidsAuthenticatedUserWithoutRequirement()
    {
        await using var app = await BuildAppAsync(
            authenticated: true,
            configure: dashboard => dashboard.RequireAuthorization("DashboardAdmin"));

        var response = await app.GetTestClient().GetAsync("/admin");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task NamedPolicyAllowsMatchingUser()
    {
        await using var app = await BuildAppAsync(
            authenticated: true,
            dashboardAdmin: true,
            configure: dashboard => dashboard.RequireAuthorization("DashboardAdmin"));

        var response = await app.GetTestClient().GetAsync("/admin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AllowAnonymousAllowsUnauthenticatedRequests()
    {
        await using var app = await BuildAppAsync(
            authenticated: false,
            configure: dashboard => dashboard.AllowAnonymous());

        var response = await app.GetTestClient().GetAsync("/admin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LastAuthorizationConfigurationWins()
    {
        await using var protectedApp = await BuildAppAsync(
            authenticated: false,
            configure: dashboard => dashboard.AllowAnonymous().RequireAuthorization("DashboardAdmin"));
        await using var anonymousApp = await BuildAppAsync(
            authenticated: false,
            configure: dashboard => dashboard.RequireAuthorization("DashboardAdmin").AllowAnonymous());

        var protectedResponse = await protectedApp.GetTestClient().GetAsync("/admin");
        var anonymousResponse = await anonymousApp.GetTestClient().GetAsync("/admin");

        Assert.Equal(HttpStatusCode.Unauthorized, protectedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, anonymousResponse.StatusCode);
    }

    [Fact]
    public async Task CustomRoutePrefixMovesAllDashboardPages()
    {
        await using var app = await BuildAppAsync(
            authenticated: true,
            configure: dashboard => dashboard.UseRoutePrefix("dashboard"));

        var client = app.GetTestClient();
        var home = await client.GetAsync("/dashboard");
        var schema = await client.GetAsync("/dashboard/data-model");
        var resource = await client.GetAsync("/dashboard/authorization-test-entities");
        var edit = await client.GetAsync("/dashboard/authorization-test-entities/edit");

        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        Assert.Equal(HttpStatusCode.OK, schema.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resource.StatusCode);
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/admin")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/admin/data-model")).StatusCode);
    }

    [Fact]
    public async Task Editor_navigation_preserves_the_originating_resource_query()
    {
        await using var app = await BuildAppAsync(authenticated: true);
        var client = app.GetTestClient();
        const string resourceUrl = "/admin/authorization-test-entities?q=needle&sort=Id&desc=true&pageNumber=2&pageSize=10";

        var resourceHtml = await client.GetStringAsync(resourceUrl);
        var newLink = Regex.Match(
            resourceHtml,
            """<a class="dash-btn dash-btn-primary" href="(?<href>[^"]+)"[^>]*>.*?New """,
            RegexOptions.Singleline);

        Assert.True(newLink.Success);
        var editorUrl = WebUtility.HtmlDecode(newLink.Groups["href"].Value);
        var editorHtml = await client.GetStringAsync(editorUrl);
        var backLink = Regex.Match(editorHtml, """<a class="dash-btn" href="(?<href>[^"]+)" data-dash-editor-back>""");
        var hiddenReturnUrl = Regex.Match(editorHtml, """<input type="hidden" id="ReturnUrl" name="ReturnUrl" value="(?<value>[^"]*)" />""");

        Assert.True(backLink.Success);
        Assert.True(hiddenReturnUrl.Success);
        Assert.Equal(resourceUrl, WebUtility.HtmlDecode(backLink.Groups["href"].Value));
        Assert.Equal(resourceUrl, WebUtility.HtmlDecode(hiddenReturnUrl.Groups["value"].Value));

        var externalReturnHtml = await client.GetStringAsync(
            "/admin/authorization-test-entities/edit?returnUrl=https%3A%2F%2Fexample.com");
        var safeBackLink = Regex.Match(externalReturnHtml, """<a class="dash-btn" href="(?<href>[^"]+)" data-dash-editor-back>""");
        Assert.Equal(
            "/admin/authorization-test-entities",
            WebUtility.HtmlDecode(safeBackLink.Groups["href"].Value));
    }

    [Fact]
    public async Task Related_record_creation_prefills_the_parent_relation_and_allows_reassignment()
    {
        await using var app = await BuildAppAsync(authenticated: true);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthorizationTestContext>();
            db.Parents.AddRange(
                new AuthorizationTestParent { Id = 1, Name = "First" },
                new AuthorizationTestParent { Id = 2, Name = "Second" });
            await db.SaveChangesAsync();
        }

        var client = app.GetTestClient();
        var listHtml = await client.GetStringAsync(
            "/admin/authorization-test-childs?filterField=ParentId&filterValue=1");
        var newLink = Regex.Match(
            listHtml,
            """<a class="dash-btn dash-btn-primary" href="(?<href>[^"]+)"[^>]*>.*?New """,
            RegexOptions.Singleline);

        Assert.True(newLink.Success);
        var editorUrl = WebUtility.HtmlDecode(newLink.Groups["href"].Value);
        Assert.Contains("parentField=ParentId", editorUrl);
        Assert.Contains("parentValue=1", editorUrl);

        var editorResponse = await client.GetAsync(editorUrl);
        var editorHtml = await editorResponse.Content.ReadAsStringAsync();
        client.DefaultRequestHeaders.Add(
            "Cookie",
            string.Join("; ", editorResponse.Headers.GetValues("Set-Cookie").Select(value => value.Split(';')[0])));
        Assert.Matches("<select[^>]+name=\"ParentId\"[^>]*>[\\s\\S]*?<option value=\"1\" selected", editorHtml);
        Assert.Contains("class=\"dash-input dash-lookup-search\"", editorHtml);

        var response = await client.PostAsync(
            editorUrl,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["ParentField"] = "ParentId",
                ["ParentValue"] = "1",
                ["ParentId"] = "2",
                ["__RequestVerificationToken"] = AntiforgeryToken(editorHtml)
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var verificationScope = app.Services.CreateAsyncScope();
        var child = await verificationScope.ServiceProvider
            .GetRequiredService<AuthorizationTestContext>().Children.SingleAsync();
        Assert.Equal(2, child.ParentId);
    }

    [Fact]
    public async Task Editor_renders_flags_json_and_semantic_text_controls()
    {
        await using var app = await BuildAppAsync(authenticated: true, configure: ConfigureEditors);

        var html = await app.GetTestClient().GetStringAsync("/admin/authorization-test-entities/edit");

        Assert.Matches("<input[^>]+name=\"Email\"[^>]+type=\"email\"", html);
        Assert.Matches("<input[^>]+name=\"Website\"[^>]+type=\"url\"", html);
        Assert.Matches("<input[^>]+name=\"AvatarUrl\"[^>]+type=\"url\"[^>]+data-image-url-input", html);
        Assert.Contains("data-image-url-preview", html);
        Assert.Matches("<input[^>]+name=\"Phone\"[^>]+type=\"tel\"", html);
        Assert.Contains("name=\"Metadata\" class=\"dash-textarea dash-json-editor\"", html);
        Assert.Contains("name=\"Permissions\" value=\"Read\"", html);
        Assert.Contains("name=\"Permissions\" value=\"Write\"", html);
    }

    [Fact]
    public async Task DateTimeOffset_editor_renders_a_valid_local_value_and_offset()
    {
        await using var app = await BuildAppAsync(authenticated: true);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var value = new DateTimeOffset(2026, 8, 22, 13, 14, 15, 123, TimeSpan.FromHours(5.5)).AddTicks(4567);
            var db = scope.ServiceProvider.GetRequiredService<AuthorizationTestContext>();
            db.Entities.Add(new AuthorizationTestEntity { Id = 1, ScheduledAt = value });
            await db.SaveChangesAsync();
        }

        var html = WebUtility.HtmlDecode(
            await app.GetTestClient().GetStringAsync("/admin/authorization-test-entities/edit?id=1"));

        Assert.Matches("<input[^>]+name=\"ScheduledAt\"[^>]+value=\"2026-08-22T13:14:15.123\"[^>]+type=\"datetime-local\"", html);
        Assert.Matches("<input[^>]+name=\"__offset_ScheduledAt\"[^>]+value=\"\\+05:30\"", html);
        Assert.DoesNotContain("2026-08-22T13:14:15.1234567", html);
    }

    [Fact]
    public async Task Image_url_editor_only_renders_http_images_in_resource_grid()
    {
        await using var app = await BuildAppAsync(authenticated: true, configure: ConfigureEditors);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthorizationTestContext>();
            db.Entities.AddRange(
                new AuthorizationTestEntity { Id = 1, AvatarUrl = "https://example.test/avatar.png" },
                new AuthorizationTestEntity { Id = 2, AvatarUrl = "javascript:alert(1)" });
            await db.SaveChangesAsync();
        }

        var html = await app.GetTestClient().GetStringAsync("/admin/authorization-test-entities");
        var safeEditHtml = await app.GetTestClient().GetStringAsync("/admin/authorization-test-entities/edit?id=1");
        var unsafeEditHtml = await app.GetTestClient().GetStringAsync("/admin/authorization-test-entities/edit?id=2");

        Assert.Contains("src=\"https://example.test/avatar.png\"", html);
        Assert.Contains("referrerpolicy=\"no-referrer\"", html);
        Assert.DoesNotContain("src=\"javascript:", html);
        Assert.Contains("src=\"https://example.test/avatar.png\"", safeEditHtml);
        Assert.DoesNotContain("src=\"javascript:", unsafeEditHtml);
    }

    [Fact]
    public async Task Editor_rejects_invalid_json_and_saves_combined_flags()
    {
        await using var app = await BuildAppAsync(authenticated: true, configure: ConfigureEditors);
        var client = app.GetTestClient();
        var editorResponse = await client.GetAsync("/admin/authorization-test-entities/edit");
        var editorHtml = await editorResponse.Content.ReadAsStringAsync();
        client.DefaultRequestHeaders.Add(
            "Cookie",
            string.Join("; ", editorResponse.Headers.GetValues("Set-Cookie").Select(value => value.Split(';')[0])));
        var token = AntiforgeryToken(editorHtml);

        var invalidResponse = await client.PostAsync(
            "/admin/authorization-test-entities/edit",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Metadata"] = "{invalid}",
                ["__RequestVerificationToken"] = token
            }));
        var invalidHtml = await invalidResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, invalidResponse.StatusCode);
        Assert.Contains("Metadata must contain valid JSON.", invalidHtml);

        var validResponse = await client.PostAsync(
            "/admin/authorization-test-entities/edit",
            new FormUrlEncodedContent([
                new KeyValuePair<string, string>("Metadata", "{\"enabled\":true}"),
                new KeyValuePair<string, string>("Permissions", nameof(TestPermissions.Read)),
                new KeyValuePair<string, string>("Permissions", nameof(TestPermissions.Write)),
                new KeyValuePair<string, string>("__RequestVerificationToken", AntiforgeryToken(invalidHtml))
            ]));

        Assert.Equal(HttpStatusCode.Redirect, validResponse.StatusCode);
        await using var scope = app.Services.CreateAsyncScope();
        var entity = await scope.ServiceProvider.GetRequiredService<AuthorizationTestContext>().Entities.SingleAsync();
        Assert.Equal("{\"enabled\":true}", entity.Metadata);
        Assert.Equal(TestPermissions.Read | TestPermissions.Write, entity.Permissions);
    }

    [Fact]
    public void CustomRoutePrefixRejectsRouteParameters()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentException>(() =>
            services.AddEFCoreDashboard<AuthorizationTestContext>(dashboard =>
                dashboard.UseRoutePrefix("/{tenant}/admin")));

        Assert.Equal("routePrefix", exception.ParamName);
    }

    private static async Task<WebApplication> BuildAppAsync(
        bool authenticated,
        bool dashboardAdmin = false,
        Action<DashboardBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });
        builder.WebHost.UseTestServer();
        var databaseName = $"dashboard-authorization-{Guid.NewGuid()}";
        builder.Services.AddDbContext<AuthorizationTestContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        builder.Services
            .AddAuthentication(TestAuthenticationHandler.SchemeName)
            .AddScheme<TestAuthenticationOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler.SchemeName,
                options =>
                {
                    options.Authenticated = authenticated;
                    options.DashboardAdmin = dashboardAdmin;
                });
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("DashboardAdmin", policy => policy.RequireClaim("dashboard", "admin"));
        builder.Services.AddEFCoreDashboard<AuthorizationTestContext>(configure);

        var app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapRazorPages();
        await app.StartAsync();
        return app;
    }

    private static void ConfigureEditors(DashboardBuilder dashboard) =>
        dashboard.Resource<AuthorizationTestEntity>(resource =>
        {
            resource.Field(x => x.Email).Editor(DashboardEditors.Email);
            resource.Field(x => x.Website).Editor(DashboardEditors.Url);
            resource.Field(x => x.AvatarUrl).Editor(DashboardEditors.ImageUrl);
            resource.Field(x => x.Phone).Editor(DashboardEditors.Telephone);
            resource.Field(x => x.Metadata).Editor(DashboardEditors.Json);
        });

    private static string AntiforgeryToken(string html)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]+value=\"(?<value>[^\"]+)\"");
        Assert.True(match.Success);
        return WebUtility.HtmlDecode(match.Groups["value"].Value);
    }

    private sealed class AuthorizationTestContext(DbContextOptions<AuthorizationTestContext> options)
        : DbContext(options)
    {
        public DbSet<AuthorizationTestEntity> Entities => Set<AuthorizationTestEntity>();
        public DbSet<AuthorizationTestParent> Parents => Set<AuthorizationTestParent>();
        public DbSet<AuthorizationTestChild> Children => Set<AuthorizationTestChild>();
    }

    private sealed class AuthorizationTestEntity
    {
        public int Id { get; set; }
        public string? Email { get; set; }
        public string? Website { get; set; }
        public string? AvatarUrl { get; set; }
        public string? Phone { get; set; }
        public string? Metadata { get; set; }
        public TestPermissions Permissions { get; set; }
        public DateTimeOffset? ScheduledAt { get; set; }
    }

    private sealed class AuthorizationTestParent
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public List<AuthorizationTestChild> Children { get; set; } = [];
    }

    private sealed class AuthorizationTestChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public AuthorizationTestParent Parent { get; set; } = null!;
    }

    [Flags]
    private enum TestPermissions
    {
        None = 0,
        Read = 1,
        Write = 2
    }

    private sealed class TestAuthenticationOptions : AuthenticationSchemeOptions
    {
        public bool Authenticated { get; set; }
        public bool DashboardAdmin { get; set; }
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<TestAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<TestAuthenticationOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Options.Authenticated)
                return Task.FromResult(AuthenticateResult.NoResult());

            var claims = new List<Claim> { new(ClaimTypes.Name, "Dashboard test user") };
            if (Options.DashboardAdmin)
                claims.Add(new Claim("dashboard", "admin"));

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
