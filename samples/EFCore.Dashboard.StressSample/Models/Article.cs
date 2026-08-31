using System.ComponentModel.DataAnnotations;

namespace EFCore.Dashboard.StressSample.Models;

public sealed class Article
{
    public int Id { get; set; }
    public Guid PublicId { get; set; } = Guid.NewGuid();
    [MaxLength(180)] public string Title { get; set; } = string.Empty;
    [MaxLength(180)] public string Slug { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public ArticleStatus Status { get; set; } = ArticleStatus.Draft;
    public bool Featured { get; set; }
    public double? Rating { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTimeOffset? ScheduledAt { get; set; }
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public int AuthorId { get; set; }
    public Author Author { get; set; } = null!;
    public int? SeriesId { get; set; }
    public Series? Series { get; set; } = null;
    public MediaAsset? Cover { get; set; } = null;
    public List<Tag> Tags { get; set; } = [];
    public List<Issue> Issues { get; set; } = [];
    public List<ArticleRevision> Revisions { get; set; } = [];
    public List<Comment> Comments { get; set; } = [];
    public List<Attachment> Attachments { get; set; } = [];
}

public enum ArticleStatus { Draft, Published, Archived }