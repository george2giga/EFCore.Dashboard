using System.ComponentModel.DataAnnotations;

namespace EFCore.Dashboard.StressSample.Models;

public sealed class Attachment
{
    public int Id { get; set; }
    [MaxLength(180)] public string Name { get; set; } = string.Empty;
    [MaxLength(20)] public string MimeType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int ArticleId { get; set; }
    public Article Article { get; set; } = null!;
}
