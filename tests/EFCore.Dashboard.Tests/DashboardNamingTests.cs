using EFCore.Dashboard.Core;
using Xunit;

namespace EFCore.Dashboard.Tests;

public sealed class DashboardNamingTests
{
    [Theory]
    [InlineData("Article", "articles")]
    [InlineData("Category", "categories")]
    [InlineData("Status", "statuses")]
    [InlineData("BlogPost", "blog-posts")]
    public void SlugFor_pluralizes_and_kebab_cases(string input, string expected)
        => Assert.Equal(expected, DashboardNaming.SlugFor(input));

    [Fact]
    public void Humanize_splits_pascal_case()
        => Assert.Equal("Created At", DashboardNaming.Humanize("CreatedAt"));
}
