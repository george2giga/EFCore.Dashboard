using System.ComponentModel.DataAnnotations;

namespace EFCore.Dashboard.StressSample.Models;

public sealed class ArticleRevision
{
    public int Id { get; set; }
    [MaxLength(180)] public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int ArticleId { get; set; }
    public Article Article { get; set; } = null!;
}