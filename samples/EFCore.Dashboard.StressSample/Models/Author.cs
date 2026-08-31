using System.ComponentModel.DataAnnotations;

namespace EFCore.Dashboard.StressSample.Models;

public sealed class Author
{
    public int Id { get; set; }
    public Guid PublicId { get; set; } = Guid.NewGuid();
    [MaxLength(120)] public string Name { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    [MaxLength(320)] public string Email { get; set; } = string.Empty;
    public AuthorStatus Status { get; set; } = AuthorStatus.Active;
    public DateOnly JoinedOn { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public int? PublisherId { get; set; }
    public Publisher? Publisher { get; set; } = null;
    public AuthorProfile? Profile { get; set; }
    public List<AuthorSkill> Skills { get; set; } = [];
    public List<Article> Articles { get; set; } = [];
}

public enum AuthorStatus { Active, Inactive, Suspended }
