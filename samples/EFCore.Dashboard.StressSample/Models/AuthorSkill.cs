using System.ComponentModel.DataAnnotations;

namespace EFCore.Dashboard.StressSample.Models;

public sealed class AuthorSkill
{
    public int Id { get; set; }
    [MaxLength(80)] public string Name { get; set; } = string.Empty;
    public Proficiency Proficiency { get; set; } = Proficiency.Junior;
    public int YearsExperience { get; set; }
    public int AuthorId { get; set; }
    public Author Author { get; set; } = null!;
}

public enum Proficiency { Junior, MidLevel, Senior, Lead }