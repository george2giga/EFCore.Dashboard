using System.ComponentModel.DataAnnotations;

namespace EFCore.Dashboard.BasicSample.Models;

public sealed class Article
{
    public int Id { get; set; }
    [MaxLength(180)] public string Title { get; set; } = string.Empty;
    [MaxLength(180)] public string Slug { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    [MaxLength(280)] public string Excerpt { get; set; } = string.Empty;
    public ArticleStatus Status { get; set; } = ArticleStatus.Draft;
    public bool Featured { get; set; }
    public int ReadingMinutes { get; set; } = 5;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
    public int AuthorId { get; set; }
    public Author Author { get; set; } = null!;
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public List<Tag> Tags { get; set; } = [];
    public List<Comment> Comments { get; set; } = [];
}

public enum ArticleStatus { Draft, Published, Archived }
