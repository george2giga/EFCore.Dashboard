using System.ComponentModel.DataAnnotations;

namespace EFCore.Dashboard.StressSample.Models;

public sealed class NewsletterSubscriber
{
    public int Id { get; set; }
    public Guid PublicId { get; set; } = Guid.NewGuid();
    [MaxLength(120)] public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime SubscribedAt { get; set; } = DateTime.UtcNow;
    public int? FavoriteArticleId { get; set; }
    public Article? FavoriteArticle { get; set; } = null;
    public List<Tag> Interests { get; set; } = [];
}