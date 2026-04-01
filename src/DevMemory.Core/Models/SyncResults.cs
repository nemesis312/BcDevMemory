namespace DevMemory.Core.Models;

public sealed class SyncExportResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ChunkId { get; set; }
    public int ExportedSessions { get; set; }
    public int ExportedObservations { get; set; }
    public int ExportedPrompts { get; set; }
    public int ExportedItems => ExportedSessions + ExportedObservations + ExportedPrompts;
}

public sealed class SyncImportResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int ProcessedChunks { get; set; }
    public int SkippedChunks { get; set; }
    public int ImportedSessions { get; set; }
    public int ImportedObservations { get; set; }
    public int ImportedPrompts { get; set; }
}
