namespace DevMemory.Core.Models;

public sealed class SyncStatus
{
    public string StorageProvider { get; set; } = string.Empty;
    public bool ProviderSupported { get; set; }
    public string ResolvedSyncPath { get; set; } = string.Empty;
    public string PathSource { get; set; } = string.Empty;
    public string ManifestPath { get; set; } = string.Empty;
    public string ChunksPath { get; set; } = string.Empty;
    public bool SyncPathExists { get; set; }
    public bool SyncPathWritable { get; set; }
    public bool ManifestExists { get; set; }
    public int? ManifestVersion { get; set; }
    public int ChunkCount { get; set; }
    public List<string> Messages { get; set; } = [];
}
