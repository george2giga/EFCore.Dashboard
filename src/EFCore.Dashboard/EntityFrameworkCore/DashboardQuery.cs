namespace EFCore.Dashboard.EntityFrameworkCore;

/// <summary>Describes search, exact filtering, ordering, and paging requested for a resource list.</summary>
public sealed record DashboardQuery(
    string? Search = null,
    string? Sort = null,
    bool Descending = false,
    int Page = 1,
    int PageSize = 20,
    IReadOnlyList<string>? VisibleFields = null,
    string? FilterField = null,
    object? FilterValue = null);

/// <summary>Contains one materialized resource page and its paging metadata.</summary>
public sealed record DashboardPage(IReadOnlyList<object> Items, int Total, int Page, int PageSize)
{
    public int PageCount => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

/// <summary>Represents a key and display label in a relationship editor.</summary>
public sealed record DashboardLookupOption(string Value, string Label);
