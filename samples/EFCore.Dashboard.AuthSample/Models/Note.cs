using System.ComponentModel.DataAnnotations;

namespace EFCore.Dashboard.AuthSample.Models;

public sealed class Note
{
    public int Id { get; set; }
    [MaxLength(160)] public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
