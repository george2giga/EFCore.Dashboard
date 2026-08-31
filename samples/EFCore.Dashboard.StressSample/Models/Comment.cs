using System.ComponentModel.DataAnnotations;

namespace EFCore.Dashboard.StressSample.Models;

public sealed class Comment
{
    public int Id { get; set; }
    [MaxLength(120)] public string AuthorName { get; set; } = string.Empty;
    [MaxLength(320)] public string AuthorEmail { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public CommentStatus Status { get; set; } = CommentStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int ArticleId { get; set; }
    public Article Article { get; set; } = null!;
}

public enum CommentStatus { Pending, Approved, Spam }