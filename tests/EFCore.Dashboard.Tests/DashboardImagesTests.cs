using EFCore.Dashboard.Web;
using Xunit;

namespace EFCore.Dashboard.Tests;

public sealed class DashboardImagesTests
{
    [Theory]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3 }, "image/png")]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3 }, "image/jpeg")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 1, 2 }, "image/gif")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50, 1, 2 }, "image/webp")]
    public void ToDataUrl_detects_supported_raster_formats(byte[] bytes, string expectedType)
    {
        var url = DashboardImages.ToDataUrl(bytes);

        Assert.NotNull(url);
        Assert.StartsWith($"data:{expectedType};base64,", url);
        Assert.Equal(Convert.ToBase64String(bytes), url[(url.IndexOf(",", StringComparison.Ordinal) + 1)..]);
        Assert.Equal(expectedType, DashboardImages.GetContentType(bytes));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(new byte[0])]
    public void ToDataUrl_returns_null_for_empty_input(byte[]? bytes) =>
        Assert.Null(DashboardImages.ToDataUrl(bytes));

    [Fact]
    public void ToDataUrl_returns_null_for_unknown_format() =>
        Assert.Null(DashboardImages.ToDataUrl(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 }));

    [Fact]
    public void ToDataUrl_returns_null_above_size_limit()
    {
        var bytes = new byte[1024 * 1024 + 1];
        bytes[0] = 0x89; bytes[1] = 0x50; bytes[2] = 0x4E; bytes[3] = 0x47;
        bytes[4] = 0x0D; bytes[5] = 0x0A; bytes[6] = 0x1A; bytes[7] = 0x0A;

        Assert.Null(DashboardImages.ToDataUrl(bytes));
    }
}
