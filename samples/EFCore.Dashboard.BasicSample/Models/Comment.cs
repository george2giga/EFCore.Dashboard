using System.ComponentModel.DataAnnotations;

namespace EFCore.Dashboard.BasicSample.Models;

public sealed class Comment
{
    public int Id { get; set; }
    [MaxLength(100)] public string AuthorName { get; set; } = string.Empty;
    [MaxLength(200)] public string AuthorEmail { get; set; } = string.Empty;
    [MaxLength(1200)] public string Body { get; set; } = string.Empty;
    public CommentStatus Status { get; set; } = CommentStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public int ArticleId { get; set; }
    public Article Article { get; set; } = null!;
}

public enum CommentStatus { Pending, Approved, Rejected }
