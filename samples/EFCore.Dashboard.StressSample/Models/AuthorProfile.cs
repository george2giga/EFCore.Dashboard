using System.ComponentModel.DataAnnotations;

namespace EFCore.Dashboard.StressSample.Models;

public sealed class AuthorProfile
{
    public int Id { get; set; }
    [MaxLength(80)] public string Location { get; set; } = string.Empty;
    public double Rating { get; set; }
    public int AuthorId { get; set; }
    public Author Author { get; set; } = null!;
}