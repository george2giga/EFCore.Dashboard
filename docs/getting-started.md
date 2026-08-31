---
layout: default
title: Getting Started
---

# Getting Started

[Configuration](configuration.md)

Add EFCore.Dashboard to an ASP.NET Core application that already uses EF Core.

## Install

```powershell
dotnet add package EFCore.Dashboard
```

The application must target .NET 8, 9, or 10 and use EF Core 9 or 10. EF Core 10 requires .NET 10. Keep using the application's existing registered `DbContext`, database provider, connection string, and migration workflow.

## Register and Configure

Register the dashboard against the existing context and protect it with a host authorization policy:

```csharp
using EFCore.Dashboard;

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("DashboardAdmin", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("dashboard", "admin"));

builder.Services.AddEFCoreDashboard<AppDbContext>(dashboard =>
{
    dashboard.RequireAuthorization("DashboardAdmin");

    dashboard.Resource<Article>(resource =>
    {
        resource.Display(article => article.Title);
        resource.Field(article => article.Content)
            .Editor(DashboardEditors.Markdown)
            .HiddenInList();
    });

    dashboard.Exclude<AuditEntry>();
});
```

`AddEFCoreDashboard<TDbContext>()` adds the dashboard services and Razor Pages. It does not configure the context, authentication, users, or policies.

Eligible entities are discovered automatically. `Resource<TEntity>()` changes dashboard presentation, while `Exclude<TEntity>()` hides an entity from the dashboard. Neither changes the EF model or database. See [Configuration](configuration.md) for all options.

If no named policy is selected, the dashboard uses the host's default authorization policy. For local evaluation only, `dashboard.AllowAnonymous()` makes every read and write operation public.

## Authentication Options

### Use the Host's Authentication

Prefer the application's existing Identity, OpenID Connect, cookie, or other authentication setup. The policy above grants access to authenticated users with the `dashboard=admin` claim.

For a minimal host-owned cookie, register a handler:

```csharp
using Microsoft.AspNetCore.Authentication.Cookies;

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options => options.LoginPath = "/login");
```

After validating credentials in the login handler, issue a cookie containing the claim:

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

var identity = new ClaimsIdentity(
    [new Claim(ClaimTypes.Name, "admin"), new Claim("dashboard", "admin")],
    CookieAuthenticationDefaults.AuthenticationScheme);

await httpContext.SignInAsync(
    CookieAuthenticationDefaults.AuthenticationScheme,
    new ClaimsPrincipal(identity));
```

### Use the Optional Cookie Package

Small applications without an identity system can use the optional single-administrator package. Install it, then replace the dashboard registration above with the package setup:

```powershell
dotnet add package EFCore.Dashboard.Authentication.Cookies
```

```csharp
using EFCore.Dashboard.Authentication.Cookies;

builder.Services.AddEFCoreDashboardCookieAuthentication(options =>
{
    options.Username = builder.Configuration["Dashboard:Username"]
        ?? throw new InvalidOperationException("Dashboard username is missing.");
    options.Password = builder.Configuration["Dashboard:Password"]
        ?? throw new InvalidOperationException("Dashboard password is missing.");
});

builder.Services.AddEFCoreDashboard<AppDbContext>(dashboard =>
    dashboard.UseCookieAuthentication());
```

`UseCookieAuthentication()` selects the package's authorization policy and adds its account control to the dashboard. Keep `UseAuthentication()` and `UseAuthorization()` in the endpoint pipeline below.

Store credentials in user secrets, environment variables, or a production secret provider. 

The package provides one credential, login and logout pages. Its account routes default to `/efcore-dashboard/account` and are independent of the dashboard route prefix.

## Map Endpoints

Use the host's normal middleware order:

```csharp
var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
```

Open `/admin` after signing in as a user who satisfies the selected policy.

## Default Routes

| Route | Purpose |
| --- | --- |
| `/admin` | Resource overview |
| `/admin/data-model` | Discovered model diagram |
| `/admin/{resourceName}` | Resource list |
| `/admin/{resourceName}/edit` | Create a record |
| `/admin/{resourceName}/edit/{id}` | Edit or delete a record |

> **Note:** The default route can be overridden with `dashboard.UseRoutePrefix("/operations/data")`. The prefix is relative to the application's `PathBase` and cannot contain route parameters.

Next, read [Configuration](configuration.md) for resource, field, editor, and route options.
