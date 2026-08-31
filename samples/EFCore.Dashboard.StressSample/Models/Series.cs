using System.ComponentModel.DataAnnotations;

namespace EFCore.Dashboard.StressSample.Models;

public sealed class Series
{
    public int Id { get; set; }
    [MaxLength(120)] public string Name { get; set; } = string.Empty;
    public int? PublisherId { get; set; }
    public Publisher? Publisher { get; set; } = null;
    public List<Article> Articles { get; set; } = [];
}