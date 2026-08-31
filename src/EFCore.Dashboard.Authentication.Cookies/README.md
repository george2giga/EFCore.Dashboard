# EFCore.Dashboard.Authentication.Cookies

Simple cookie authentication for one dashboard administrator. Use it for small or internal applications that do not already have an identity system.

## Install

```powershell
dotnet add package EFCore.Dashboard.Authentication.Cookies
```

## Configure

```csharp
builder.Services.AddEFCoreDashboardCookieAuthentication(options =>
{
    options.Username = builder.Configuration["Dashboard:Username"]!;
    options.Password = builder.Configuration["Dashboard:Password"]!;
    options.RoutePrefix = "/dashboard/account";
});

builder.Services.AddEFCoreDashboard<AppDbContext>(dashboard =>
    dashboard.UseCookieAuthentication());

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
```

Store the username and password in environment variables, .NET user secrets, or another secret provider. Do not commit them to `appsettings.json`. Passwords must contain at least 12 characters.

## What It Does

- Adds login and logout pages.
- Uses an isolated ASP.NET Core authentication cookie.
- Protects the dashboard when `UseCookieAuthentication()` is enabled.
- Throttles repeated login attempts per application instance.

## What It Does Not Do

- Manage multiple users, registration, or password recovery.
- Provide roles, MFA, or an audit trail.
- Replace ASP.NET Core Identity or an external identity provider.
- Share login throttling between application instances.

## Production Notes

- HTTPS is required by default. Use `AllowInsecureHttp = true` only for local development.
- Configure forwarded headers when HTTPS terminates at a reverse proxy.
- Persist ASP.NET Core Data Protection keys if cookies must survive restarts or work across multiple instances.
