namespace EFCore.Dashboard.Web;

/// <summary>Recognizes supported image values and renders data URIs for edit previews.</summary>
public static class DashboardImages
{
    internal static string? GetSafeRemoteUrl(string? value)
    {
        var candidate = value?.Trim();
        if (string.IsNullOrEmpty(candidate) ||
            !Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
            string.IsNullOrEmpty(uri.Host) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return null;

        return candidate;
    }

    /// <summary>
    /// Returns a <c>data:image/...</c> URI when the bytes look like a supported raster image and
    /// fit within <paramref name="maxBytes"/>, otherwise returns <see langword="null"/>. The cap
    /// keeps edit previews light; uploaded values themselves are never capped here.
    /// </summary>
    public static string? ToDataUrl(byte[]? bytes, int maxBytes = 1_048_576)
    {
        if (bytes is null || bytes.Length == 0 || bytes.Length > maxBytes)
            return null;

        var contentType = GetContentType(bytes);
        if (contentType is null)
            return null;

        return $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";
    }

    /// <summary>Returns the MIME type when the bytes have a supported raster image signature.</summary>
    public static string? GetContentType(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
            return null;
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "image/jpeg";
        if (bytes.Length >= 4 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38)
            return "image/gif";
        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
            bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
            return "image/png";
        if (bytes.Length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
            bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
            return "image/webp";
        return null;
    }
}
