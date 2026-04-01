namespace DevMemory.Core.Models;

public sealed class SyncManifest
{
    public int Version { get; set; } = 1;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<SyncChunkEntry> Chunks { get; set; } = [];
}

public sealed class SyncChunkEntry
{
    public string Id { get; set; } = string.Empty;
    public string? Project { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime MaxObservationCreatedAt { get; set; }
    public int ItemCount { get; set; }
}
