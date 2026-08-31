using Microsoft.EntityFrameworkCore;
using EFCore.Dashboard;
using EFCore.Dashboard.StressSample.Data;
using EFCore.Dashboard.StressSample.Models;

var builder = WebApplication.CreateBuilder(args);

// Keep referenced Razor class library assets available when running the sample
// from source without a Development launch profile.
builder.WebHost.UseStaticWebAssets();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=efcore-dashboard-stress-sample.db"));

builder.Services.AddEFCoreDashboard<AppDbContext>(dash =>
{
    dash.AllowAnonymous();

    dash.Resource<Article>(resource =>
    {
        resource.Label("Articles");
        resource.Display(x => x.Title);
        resource.Field(x => x.PublicId).Hidden();
        resource.Field(x => x.Content).Editor(DashboardEditors.Markdown).HiddenInList();
        resource.Field(x => x.CreatedAt).ReadOnly();
    });

    dash.Resource<Category>(resource =>
    {
        resource.Label("Categories");
        resource.Display(x => x.Name);
        resource.Field(x => x.PublicId).Hidden();
    });

    dash.Resource<Tag>(resource =>
    {
        resource.Label("Tags");
        resource.Display(x => x.Name);
    });

    dash.Resource<Author>(resource =>
    {
        resource.Label("Authors");
        resource.Display(x => x.Name);
        resource.Field(x => x.PublicId).Hidden();
        resource.Field(x => x.Email).Editor(DashboardEditors.Email);
        resource.Field(x => x.Bio).Editor(DashboardEditors.Textarea);
        resource.Field(x => x.JoinedOn).ReadOnly();
    });

    dash.Resource<AuthorProfile>(resource =>
    {
        resource.Label("Author Profiles");
        resource.Display(x => x.Location);
    });

    dash.Resource<AuthorSkill>(resource =>
    {
        resource.Label("Author Skills");
        resource.Display(x => x.Name);
    });

    dash.Resource<Publisher>(resource =>
    {
        resource.Label("Publishers");
        resource.Display(x => x.Name);
        resource.Field(x => x.Url).Editor(DashboardEditors.Url);
    });

    dash.Resource<Series>(resource =>
    {
        resource.Label("Series");
        resource.Display(x => x.Name);
    });

    dash.Resource<ArticleRevision>(resource =>
    {
        resource.Label("Article Revisions");
        resource.Display(x => x.Title);
        resource.Field(x => x.Body).Editor(DashboardEditors.Textarea);
        resource.Field(x => x.CreatedAt).ReadOnly();
    });

    dash.Resource<Comment>(resource =>
    {
        resource.Label("Comments");
        resource.Display(x => x.AuthorName);
        resource.Field(x => x.AuthorEmail).Editor(DashboardEditors.Email);
        resource.Field(x => x.Body).Editor(DashboardEditors.Textarea);
        resource.Field(x => x.CreatedAt).ReadOnly();
    });

    dash.Resource<Attachment>(resource =>
    {
        resource.Label("Attachments");
        resource.Display(x => x.Name);
    });

    dash.Resource<MediaAsset>(resource =>
    {
        resource.Label("Media Assets");
        resource.Display(x => x.Url);
        resource.Field(x => x.Url).Editor(DashboardEditors.Url);
    });

    dash.Resource<Issue>(resource =>
    {
        resource.Label("Issues");
        resource.Display(x => x.Name);
    });

    dash.Resource<NewsletterSubscriber>(resource =>
    {
        resource.Label("Newsletter Subscribers");
        resource.Display(x => x.Email);
        resource.Field(x => x.PublicId).Hidden();
        resource.Field(x => x.Email).Editor(DashboardEditors.Email);
        resource.Field(x => x.SubscribedAt).ReadOnly();
    });
});

var app = builder.Build();
app.MapStaticAssets();
app.UseRouting();
app.MapRazorPages();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (Environment.GetEnvironmentVariable("RESET_DATABASE") == "1")
        await db.Database.EnsureDeletedAsync();
    await db.Database.EnsureCreatedAsync();
    await SeedData.InitializeAsync(db);
}

app.MapGet("/", () => Results.Content("""
<!doctype html>
<html lang="en">
<head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><meta name="color-scheme" content="light dark"><title>EFCore.Dashboard stress sample</title></head>
<body style="font-family:system-ui;padding:48px;max-width:760px;margin:auto;line-height:1.5">
<h1>EFCore.Dashboard stress sample</h1>
<p><strong>Do not deploy as configured:</strong> this sample exposes anonymous CRUD operations over 14 related entity types.</p>
<p>Set <code>SEED_SCALE</code> from <code>0.01</code> to <code>10</code> to control the dataset size. Set <code>RESET_DATABASE=1</code> once to recreate the database after model changes.</p>
<p><a href="/admin">Open EFCore.Dashboard</a></p>
</body>
</html>
""", "text/html"));

app.Run();
