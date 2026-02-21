namespace DevMemory.Core.Entities;

public class Session
{
    public Guid Id { get; set; }
    public string? Project { get; set; }
    public string? Goal { get; set; }
    public string? Summary { get; set; }
    public string[] FilesChanged { get; set; } = Array.Empty<string>();
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public bool IsActive { get; set; }

    // Navigation
    public List<Observation> Observations { get; set; } = new();
}
