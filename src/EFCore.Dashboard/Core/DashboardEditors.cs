namespace EFCore.Dashboard;

/// <summary>Names of editors supported by the built-in dashboard form.</summary>
public static class DashboardEditors
{
    public const string Textarea = "textarea";
    public const string Markdown = "markdown";
    public const string RichText = "richtext";
    public const string Image = "image";
    public const string Json = "json";
    public const string Email = "email";
    public const string Url = "url";
    public const string ImageUrl = "imageurl";
    public const string Telephone = "tel";

    internal static string Normalize(string editor) => editor.ToLowerInvariant() switch
    {
        Textarea => Textarea,
        Markdown => Markdown,
        RichText => RichText,
        Image => Image,
        Json => Json,
        Email => Email,
        Url => Url,
        ImageUrl => ImageUrl,
        Telephone => Telephone,
        _ => throw new ArgumentException($"'{editor}' is not a supported dashboard editor.", nameof(editor))
    };
}
