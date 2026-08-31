using System.ComponentModel.DataAnnotations;

namespace EFCore.Dashboard.StressSample.Models;

public sealed class Publisher
{
    public int Id { get; set; }
    [MaxLength(120)] public string Name { get; set; } = string.Empty;
    [MaxLength(200)] public string Url { get; set; } = string.Empty;
    public List<Author> Authors { get; set; } = [];
    public List<Series> Series { get; set; } = [];
}