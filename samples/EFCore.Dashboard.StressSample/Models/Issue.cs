using System.ComponentModel.DataAnnotations;

namespace EFCore.Dashboard.StressSample.Models;

public sealed class Issue
{
    public int Id { get; set; }
    [MaxLength(120)] public string Name { get; set; } = string.Empty;
    public DateOnly? PublishedOn { get; set; }
    public IssueStatus Status { get; set; } = IssueStatus.Draft;
    public List<Article> Articles { get; set; } = [];
}

public enum IssueStatus { Draft, Published, Archived }
