using System.ComponentModel.DataAnnotations;

namespace EFCore.Dashboard.StressSample.Models;

public sealed class MediaAsset
{
    public int Id { get; set; }
    [MaxLength(200)] public string Url { get; set; } = string.Empty;
    [MaxLength(20)] public string MimeType { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public MediaKind Kind { get; set; } = MediaKind.Photo;
    public int? CoverArticleId { get; set; }
    public Article? CoverArticle { get; set; } = null;
}

public enum MediaKind { Photo, Illustration, Diagram }