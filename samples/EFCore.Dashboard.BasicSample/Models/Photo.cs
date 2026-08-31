using System.ComponentModel.DataAnnotations;

namespace EFCore.Dashboard.BasicSample.Models;

public sealed class Photo
{
    public int Id { get; set; }
    [MaxLength(120)] public string Title { get; set; } = string.Empty;
    public byte[] Image { get; set; } = [];
    [MaxLength(500)] public string Caption { get; set; } = string.Empty;
    [MaxLength(100)] public string Credit { get; set; } = string.Empty;
    public DateOnly CapturedOn { get; set; }
    public bool Published { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}
