# EFCore.Dashboard engineering guide

Build EFCore.Dashboard as a small, idiomatic .NET library. Prefer framework capabilities over abstractions.

## Principles

- Target .NET 8, 9, and 10 with EF Core 9 and 10 using current ASP.NET Core / EF Core conventions.
- Keep the consumer API minimal and discoverable.
- Convention first, configuration second, extension points only for concrete use cases.
- Keep EF Core details behind EFCore.Dashboard metadata/repository boundaries.
- Razor + HTMX owns server state; JavaScript is progressive enhancement only.
- No MediatR, AutoMapper, generic repository frameworks, SPA framework, or runtime Node dependency.
- Avoid speculative abstractions. Add extension points where there is a concrete use case.
- Keep classes small, names explicit, and code easy to debug.
- Use async I/O for database writes and materialization.
- Preserve host application behavior: EFCore.Dashboard should not replace auth, logging, routing, or database configuration.

## Quality bar

Before completing a change, run:

```bash
dotnet format --verify-no-changes
dotnet test
dotnet pack src/EFCore.Dashboard/EFCore.Dashboard.csproj -c Release
```

Add tests for metadata/value conversion behavior and integration tests for important CRUD paths as the project grows.
