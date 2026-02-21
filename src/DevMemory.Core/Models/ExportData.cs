namespace DevMemory.Core.Models;

/// <summary>
/// Root envelope for JSON export/import.
/// </summary>
public sealed class ExportData
{
    public string Version { get; set; } = "1.0";
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    public string? Project { get; set; }
    public List<ExportedObservation> Observations { get; set; } = new();
}

public sealed class ExportedObservation
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Project { get; set; }
    public string[] Tags { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}
