using System.ComponentModel.DataAnnotations;

namespace EFCore.Dashboard.BasicSample.Models;

public sealed class Category
{
    public int Id { get; set; }
    [MaxLength(80)] public string Name { get; set; } = string.Empty;
    public List<Article> Articles { get; set; } = [];
}
