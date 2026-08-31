using System.ComponentModel.DataAnnotations;

namespace EFCore.Dashboard.BasicSample.Models;

public sealed class Tag
{
    public int Id { get; set; }
    [MaxLength(40)] public string Name { get; set; } = string.Empty;
    public List<Article> Articles { get; set; } = [];
}