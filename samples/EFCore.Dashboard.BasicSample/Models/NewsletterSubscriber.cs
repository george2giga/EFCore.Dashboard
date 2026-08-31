using System.ComponentModel.DataAnnotations;

namespace EFCore.Dashboard.BasicSample.Models;

public sealed class NewsletterSubscriber
{
    public int Id { get; set; }
    [MaxLength(200)] public string Email { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
    public DateTimeOffset SubscribedAt { get; set; } = DateTimeOffset.UtcNow;
}
