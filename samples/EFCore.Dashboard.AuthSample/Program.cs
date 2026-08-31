using EFCore.Dashboard;
using EFCore.Dashboard.Authentication.Cookies;
using EFCore.Dashboard.AuthSample.Data;
using EFCore.Dashboard.AuthSample.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Keep referenced Razor class library assets available when running from source.
builder.WebHost.UseStaticWebAssets();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=efcore-dashboard-auth-sample.db"));

// Fixed credentials and insecure HTTP keep the local sample easy to run.
// Replace both before deploying an application based on this sample.
builder.Services.AddEFCoreDashboardCookieAuthentication(options =>
{
    options.Username = "demo-administrator";
    options.Password = "dashboard-demo-password";
    options.AllowInsecureHttp = true;
});

builder.Services.AddEFCoreDashboard<AppDbContext>(dashboard =>
{
    dashboard.UseCookieAuthentication();
    dashboard.AddStylesheet("~/auth-sample.css");
    dashboard.Resource<Note>(resource =>
    {
        resource.Display(note => note.Title);
        resource.Field(note => note.Content).Editor(DashboardEditors.Textarea);
        resource.Field(note => note.CreatedAt).ReadOnly();
    });
});

var app = builder.Build();
app.MapStaticAssets();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    if (!await db.Notes.AnyAsync())
    {
        db.Notes.Add(new Note
        {
            Title = "Authenticated dashboard",
            Content = "This record is available after a dashboard administrator signs in.",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }
}

app.MapGet("/", () => Results.Content("""
<!doctype html>
<html lang="en">
<head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><meta name="color-scheme" content="light dark"><title>Authenticated dashboard sample</title></head>
<body style="font-family:system-ui;padding:48px;max-width:760px;margin:auto;line-height:1.5">
<h1>Authenticated dashboard sample</h1>
<p><strong>Demo configuration:</strong> insecure HTTP and a source-controlled credential are enabled to make the optional cookie package easy to try. Production credentials belong in a secret provider and require HTTPS.</p>
<p>Username: <code>demo-administrator</code><br>Password: <code>dashboard-demo-password</code></p>
<p><a href="/admin">Open the protected dashboard</a></p>
<p><a href="/efcore-dashboard/account/login">Sign in</a></p>
</body>
</html>
""", "text/html"));

app.Run();
