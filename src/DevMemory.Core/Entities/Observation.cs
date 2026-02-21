namespace DevMemory.Core.Entities;

public class Observation
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Project { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public DateTime CreatedAt { get; set; }

    // Populated on search results only
    public float? Rank { get; set; }
    public string? ContentPreview { get; set; }
}
