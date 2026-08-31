using System.ComponentModel.DataAnnotations;

namespace EFCore.Dashboard.BasicSample.Models;

public sealed class Author
{
    public int Id { get; set; }
    [MaxLength(100)] public string Name { get; set; } = string.Empty;
    [MaxLength(100)] public string Slug { get; set; } = string.Empty;
    [MaxLength(100)] public string Role { get; set; } = string.Empty;
    [MaxLength(280)] public string Bio { get; set; } = string.Empty;
    [MaxLength(200)] public string Email { get; set; } = string.Empty;
    [MaxLength(500)] public string Website { get; set; } = string.Empty;
    [MaxLength(500)] public string AvatarUrl { get; set; } = string.Empty;
    public bool AvailableForWork { get; set; }
    public DateOnly JoinedOn { get; set; }
    public List<Article> Articles { get; set; } = [];
}
