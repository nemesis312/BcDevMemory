namespace DevMemory.Core.Models;

public sealed class SyncChunkDocument
{
    public int Version { get; set; } = 1;
    public string ChunkId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? Project { get; set; }
    public List<SyncSessionRecord> Sessions { get; set; } = [];
    public List<SyncObservationRecord> Observations { get; set; } = [];
    public List<SyncPromptRecord> Prompts { get; set; } = [];
}

public sealed class SyncSessionRecord
{
    public Guid Id { get; set; }
    public string? Project { get; set; }
    public string? Goal { get; set; }
    public string? Summary { get; set; }
    public string[] FilesChanged { get; set; } = [];
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public bool IsActive { get; set; }
}

public sealed class SyncObservationRecord
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

public sealed class SyncPromptRecord
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
