---
layout: default
title: Configuration
---

# Configuration

[Getting started](getting-started.md)

EFCore.Dashboard derives its defaults from `DbContext.Model`. Configure only the resources and fields that need different presentation or editing behavior.

## Model Discovery

EFCore.Dashboard discovers eligible entity types from `DbContext.Model`. Eligible types must have a CLR type and a single-column primary key, and cannot be owned or shared-type entities.

Each discovered type becomes a `DashboardResource`, EFCore.Dashboard's metadata representation of an entity type. The host application can customize its labels, fields, and editors at startup with `Resource<TEntity>()`, or remove it from the dashboard with `Exclude<TEntity>()`.

CRUD operations use the host application's configured `DbContext`. Queries are composed as `IQueryable` expressions, allowing EF Core to translate sorting, paging, searching, and filtering for execution by the configured database provider.

## Resources

By convention, the dashboard discovers eligible entities, derives labels from CLR type names, orders resources by label, and chooses a display field in this order: configured property, `Name`, `Title`, first non-key string, then primary key.

```csharp
dashboard.Resource<Customer>(resource =>
{
    resource.Label("Customers");
    resource.Display(customer => customer.DisplayName);
});

dashboard.Exclude<AuditEntry>();
```

`Display` must select a mapped property directly. It controls relationship labels and the default list sort. `Exclude` affects dashboard discovery only.

## Fields

```csharp
dashboard.Resource<Article>(resource =>
{
    resource.Field(x => x.Title).Label("Headline");
    resource.Field(x => x.SecretToken).Hidden();
    resource.Field(x => x.Content).HiddenInList();
    resource.Field(x => x.CreatedAt).HiddenInEditor();
    resource.Field(x => x.ExternalId).ReadOnly();
});
```

| Option | Effect |
| --- | --- |
| `Hidden()` | Removes the field from lists, search, CSV, forms, and the diagram |
| `HiddenInList()` | Removes the field from lists, search, and CSV |
| `HiddenInEditor()` | Removes the field from create and edit forms |
| `ReadOnly()` | Displays the field without assigning submitted values |

Store-generated fields are read-only by convention. Primary keys are never changed during updates.

## Editors

Inside `Resource<TEntity>()`, use `DashboardEditors` constants instead of string literals:

```csharp
resource.Field(x => x.Notes).Editor(DashboardEditors.Textarea);
resource.Field(x => x.Biography).Editor(DashboardEditors.Markdown);
resource.Field(x => x.FormattedBiography).Editor(DashboardEditors.RichText);
resource.Field(x => x.SettingsJson).Editor(DashboardEditors.Json);
resource.Field(x => x.Email).Editor(DashboardEditors.Email);
resource.Field(x => x.Website).Editor(DashboardEditors.Url);
resource.Field(x => x.AvatarUrl).Editor(DashboardEditors.ImageUrl);
resource.Field(x => x.Telephone).Editor(DashboardEditors.Telephone);
resource.Field(x => x.Avatar).Editor(DashboardEditors.Image);
```

`Textarea`, `Markdown`, `RichText`, `Json`, `Email`, `Url`, `ImageUrl`, and `Telephone` apply to strings. `Image` opts a `byte[]` property into upload and preview support.

Markdown stores raw source, rich text stores submitted HTML. Image uploads are limited to 10 MB but are not validated as image content.

## Dashboard Options

```csharp
builder.Services.AddEFCoreDashboard<AppDbContext>(dashboard =>
{
    dashboard
        .UseRoutePrefix("/operations/data")
        .RequireAuthorization("DashboardAdmin")
        .AddStylesheet("~/css/dashboard-brand.css")
        .AccountPartial("/Pages/Shared/_DashboardAccount.cshtml");
});
```

| Option | Purpose |
| --- | --- |
| `RequireAuthorization(name)` | Uses a named host policy; otherwise the default policy applies |
| `AllowAnonymous()` | Disables authorization; use only for local evaluation |
| `UseRoutePrefix(path)` | Moves dashboard routes from `/admin` to a literal path |
| `AddStylesheet(path)` | Loads a host stylesheet after built-in styles, in registration order |
| `AccountPartial(path)` | Renders a host Razor partial in the top bar |

The account partial is suitable for a username, tenant selector, or POST logout form. It appears in the top-right corner of the dashboard; nothing is rendered by default. Route prefixes are relative to the host application's `PathBase` and cannot contain parameters.

## Advanced Customization

Most applications do not need this section. Use the resource and field options above unless you need a reusable convention or must replace part of the dashboard's built-in behavior.

### Reusable Field Conventions

An `IDashboardFieldProvider` decides how an EF property should appear in the dashboard. For example, a provider could use the email editor for every string property whose name ends in `Email`. This is useful when the same rule applies across many entities. For one or two fields, use `resource.Field(...)` instead.

Register a provider during dashboard configuration:

```csharp
dashboard.AddFieldProvider<EmailFieldProvider>();
```

The first provider that accepts a property creates its dashboard field. Custom providers run before the built-in fallback. Providers are shared for the application lifetime, so they must be thread-safe.

### Replacing Built-in Services

For low-level integrations, these services control the main parts of the dashboard:

| Service | Lifetime | Responsibility |
| --- | --- | --- |
| `IDashboardResourceProvider` | Singleton | Builds resource metadata |
| `IDashboardValueConverter` | Singleton | Formats and parses values |
| `IDashboardRepository` | Scoped | Performs queries and writes |

You can replace one after calling `AddEFCoreDashboard`, but this is intended for integrations that cannot be implemented with normal configuration or a field provider. A replacement must preserve the behavior expected by the built-in UI, including key handling, asynchronous database access, and query filters.

For example, a host can replace the built-in repository with its own `AuditedDashboardRepository`:

```csharp
using EFCore.Dashboard.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

builder.Services.AddEFCoreDashboard<AppDbContext>();
builder.Services.Replace(
    ServiceDescriptor.Scoped<IDashboardRepository, AuditedDashboardRepository>());
```

`AuditedDashboardRepository` is implemented by the host application and must implement `IDashboardRepository`.

Complete all configuration before the dashboard is first used. Metadata is cached for the application lifetime, so restart the application after changing the EF model, dashboard configuration, field providers, or service replacements.
