using EFCore.Dashboard.Web;
using Xunit;

namespace EFCore.Dashboard.Tests;

public sealed class DashboardCsvTests
{
    [Fact]
    public void Escapes_plain_values_without_quotes()
    {
        Assert.Equal("Hello", DashboardCsv.Escape("Hello"));
    }

    [Fact]
    public void Escapes_values_containing_commas()
    {
        Assert.Equal("\"Hello, world\"", DashboardCsv.Escape("Hello, world"));
    }

    [Fact]
    public void Escapes_values_containing_quotes_by_doubling_them()
    {
        Assert.Equal("\"say \"\"hi\"\"\"", DashboardCsv.Escape("say \"hi\""));
    }

    [Fact]
    public void Escapes_values_containing_newlines()
    {
        Assert.Equal("\"line1\nline2\"", DashboardCsv.Escape("line1\nline2"));
    }

    [Fact]
    public void Serializes_rows_with_quoted_headers()
    {
        var rows = new List<IReadOnlyList<string>>();
        rows.Add(new[] { "Title, with comma", "Author" });
        rows.Add(new[] { "A \"quote\"", "Jane" });
        Assert.Equal("\"Title, with comma\",Author\r\n\"A \"\"quote\"\"\",Jane", DashboardCsv.Serialize(rows));
    }

    [Fact]
    public void Serializes_empty_rows()
    {
        var rows = new List<IReadOnlyList<string>>();
        rows.Add(new string[0]);
        rows.Add(new[] { "only header" });
        Assert.Equal("\r\nonly header", DashboardCsv.Serialize(rows));
    }
}