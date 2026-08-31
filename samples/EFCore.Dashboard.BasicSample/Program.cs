using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using EFCore.Dashboard;
using EFCore.Dashboard.BasicSample.Data;
using EFCore.Dashboard.BasicSample.Models;

var builder = WebApplication.CreateBuilder(args);

// Keep referenced Razor class library assets available when running the sample
// from source without a Development launch profile.
builder.WebHost.UseStaticWebAssets();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=efcore-dashboard-sample.db"));

// Simplest possible login: a demo cookie issued by the sample, no accounts.
// The dashboard's /admin pages require the host's default authorization policy,
// which this cookie scheme satisfies once signed in.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options => options.LoginPath = "/login");

builder.Services.AddEFCoreDashboard<AppDbContext>(dash =>
{
    dash.AccountPartial("/Pages/Shared/_DashboardAccount.cshtml");

    dash.Resource<Article>(resource =>
    {
        resource.Label("Articles");
        resource.Display(x => x.Title);
        resource.Field(x => x.Content).Editor(DashboardEditors.Textarea).HiddenInList();
        resource.Field(x => x.CreatedAt).HiddenInEditor();
        resource.Field(x => x.Excerpt).HiddenInList();
        resource.Field(x => x.Slug).HiddenInList();
        resource.Field(x => x.ReadingMinutes).HiddenInList();
    });

    dash.Resource<Author>(resource =>
    {
        resource.Display(x => x.Name);
        resource.Field(x => x.Bio).Editor(DashboardEditors.Textarea);
        resource.Field(x => x.Email).Editor(DashboardEditors.Email);
        resource.Field(x => x.Website).Editor(DashboardEditors.Url);
        resource.Field(x => x.AvatarUrl).Editor(DashboardEditors.ImageUrl);
    });

    dash.Resource<Category>(resource =>
    {
        resource.Label("Categories");
        resource.Display(x => x.Name);
    });

    dash.Resource<Tag>(resource =>
    {
        resource.Label("Tags");
        resource.Display(x => x.Name);
    });

    dash.Resource<Comment>(resource =>
    {
        resource.Display(x => x.AuthorName);
        resource.Field(x => x.AuthorEmail).Editor(DashboardEditors.Email);
        resource.Field(x => x.Body).Editor(DashboardEditors.Textarea);
        resource.Field(x => x.CreatedAt).ReadOnly();
    });

    dash.Resource<Photo>(resource =>
    {
        resource.Label("Photo Library");
        resource.Display(x => x.Title);
        resource.Field(x => x.Image).Editor(DashboardEditors.Image);
        resource.Field(x => x.Caption).Editor(DashboardEditors.Textarea).HiddenInList();
        resource.Field(x => x.UploadedAt).Hidden();
    });

    dash.Resource<NewsletterSubscriber>(resource =>
    {
        resource.Label("Newsletter Subscribers");
        resource.Display(x => x.Email);
        resource.Field(x => x.Email).Editor(DashboardEditors.Email);
        resource.Field(x => x.SubscribedAt).Hidden();
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
    await SeedData.InitializeAsync(db);
}

app.Run();
