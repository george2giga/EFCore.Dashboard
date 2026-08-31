![EFCore.Dashboard](https://raw.githubusercontent.com/george2giga/EFCore.Dashboard/master/docs/assets/efcore-dashboard-logo.svg)

# EFCore.Dashboard

A NuGet package that adds an admin dashboard to existing ASP.NET Core projects using Entity Framework Core.
Built with Razor Pages and HTMX, the dashboard provides CRUD operations over an existing `DbContext`.

**[Try the live demo](https://efcoredashboard-demo.up.railway.app/login)** - Sign in with one click to explore the dashboard. The shared demo data resets every six hours.

## Features
✅ Create, edit, individual and bulk delete entities.  
✅ Search, scalar sorting, pagination, relationship filters, and CSV export.  
✅ Enum, flags-enum, and single-column foreign-key editors.  
✅ Binary image upload, image URL, JSON, email, URL, telephone, Markdown, and rich-text editors.  
✅ Lazy-loaded image thumbnails with upload and remote URL previews.  
✅ Interactive EF model diagram, including many-to-many relationships.  
✅ Responsive table and card views with light and dark themes.  
✅ Convention-first resource and field configuration.  
✅ Host authorization, routing, styles, and account-control integration.  
✅ Embedded static resources, no CDN or Node dependencies.

## Not Supported

❌ Composite primary keys or composite foreign-key editors.  
❌ Owned and shared-type entities as standalone dashboard resources.  
❌ Editing collection navigations or many-to-many associations.  

![EFCore.Dashboard article management](https://raw.githubusercontent.com/george2giga/EFCore.Dashboard/master/docs/assets/dashboard-articles.png)

## Install

```powershell
dotnet add package EFCore.Dashboard
```

EFCore.Dashboard supports .NET 8, 9, and 10. A registered `DbContext` is required.


## Minimal example

Register the dashboard against an existing `DbContext`:

> **Warning:** `AllowAnonymous()` exposes all dashboard read and write operations. Use it only for local evaluation. For an authenticated setup, follow the [Getting Started guide](docs/getting-started.md).

```csharp
using EFCore.Dashboard;

builder.Services.AddEFCoreDashboard<AppDbContext>(dashboard =>
{
    dashboard.AllowAnonymous();

    dashboard.Resource<Article>(resource =>
    {
        resource.Display(article => article.Title);
        resource.Field(article => article.Content)
            .Editor(DashboardEditors.Markdown)
            .HiddenInList();
        resource.Field(article => article.CoverImage)
            .Editor(DashboardEditors.Image);
    });    
});
```

Map the packaged assets and Razor Pages:

```csharp
var app = builder.Build();

app.UseStaticFiles();
app.MapRazorPages();

app.Run();
```

Open `/admin` to explore the dashboard.

[Follow the complete getting-started guide](docs/getting-started.md).

## Security

The core package does not create users, cookies, policies, or a login flow. It uses the host application's existing authentication.

Small applications without an identity system can install `EFCore.Dashboard.Authentication.Cookies`, an optional single-administrator login package.
It is intentionally not a replacement for ASP.NET Core Identity or an external identity provider.

[Authentication options](https://github.com/george2giga/EFCore.Dashboard/blob/master/docs/getting-started.md#authentication-options)

## Samples

| Sample | Audience           | Purpose |
| --- |--------------------| --- |
| [`EFCore.Dashboard.BasicSample`](samples/EFCore.Dashboard.BasicSample) | Primary sample     | Host authentication, resource configuration, relationships, editors, and images |
| [`EFCore.Dashboard.AuthSample`](samples/EFCore.Dashboard.AuthSample) | Cookie sample      | Optional single-administrator cookie authentication package |
| [`EFCore.Dashboard.StressSample`](samples/EFCore.Dashboard.StressSample) | High volume sample | Large seeded model, relationship coverage, pagination, and high-volume data |

Run the primary sample from the repository root:

```powershell
dotnet run --project samples/EFCore.Dashboard.BasicSample
```
