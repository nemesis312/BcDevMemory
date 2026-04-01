namespace DevMemory.Infrastructure.Sync;

public sealed class SyncRuntimeOptions
{
    public string StorageProvider { get; set; } = "SQLite";
    public string? ConfiguredSyncPath { get; set; }
}
