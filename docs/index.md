---
layout: default
title: EFCore.Dashboard
---

![EFCore.Dashboard](assets/efcore-dashboard-logo.svg)

EFCore.Dashboard adds a convention-first admin dashboard to ASP.NET Core applications backed by Entity Framework Core. It provides CRUD operations, search, sorting, filtering, CSV export, relationship navigation, and a responsive Razor Pages and HTMX interface over an existing `DbContext`.

## Get Started

Install the package:

```powershell
dotnet add package EFCore.Dashboard
```

Follow the [getting-started guide](getting-started.md) to register the dashboard, configure authorization, and map the required endpoints.

## Documentation

- [Getting started](getting-started.md): installation, registration, routing, and authentication
- [Configuration](configuration.md): resources, fields, editors, dashboard options, and advanced customization

## Requirements

- .NET 8, 9, or 10
- Entity Framework Core 9, or Entity Framework Core 10 on .NET 10
- ASP.NET Core Razor Pages
- A registered `DbContext`

EFCore.Dashboard uses the host application's existing database configuration, authentication, authorization, logging, and routing.

[View the source on GitHub](https://github.com/george2giga/EFCore.Dashboard)
