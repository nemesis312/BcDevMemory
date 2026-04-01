using System.Text.Json;
using System.IO.Compression;
using DevMemory.Core.Interfaces;
using DevMemory.Core.Models;

namespace DevMemory.Infrastructure.Sync;

public sealed class FileSyncTransport : ISyncTransport
{
    private readonly string _syncRootPath;
    private readonly string _manifestPath;
    private readonly string _chunksPath;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public FileSyncTransport(string syncRootPath)
    {
        _syncRootPath = syncRootPath;
        _manifestPath = Path.Combine(syncRootPath, "manifest.json");
        _chunksPath = Path.Combine(syncRootPath, "chunks");
    }

    public async Task<SyncManifest> ReadManifestAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_manifestPath))
            return new SyncManifest { Version = 1, UpdatedAt = DateTime.UtcNow, Chunks = [] };

        var json = await File.ReadAllTextAsync(_manifestPath, cancellationToken);
        var manifest = JsonSerializer.Deserialize<SyncManifest>(json, JsonOpts);
        return manifest ?? new SyncManifest { Version = 1, UpdatedAt = DateTime.UtcNow, Chunks = [] };
    }

    public async Task WriteManifestAsync(SyncManifest manifest, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_syncRootPath);
        var json = JsonSerializer.Serialize(manifest, JsonOpts);
        await File.WriteAllTextAsync(_manifestPath, json, cancellationToken);
    }

    public async Task WriteChunkAsync(string chunkId, byte[] data, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_chunksPath);
        var chunkPath = Path.Combine(_chunksPath, $"{chunkId}.json.gz");

        await using var fileStream = File.Create(chunkPath);
        await using var gzipStream = new GZipStream(fileStream, CompressionLevel.SmallestSize);
        await gzipStream.WriteAsync(data, cancellationToken);
    }

    public async Task<byte[]> ReadChunkAsync(string chunkId, CancellationToken cancellationToken = default)
    {
        var chunkPath = Path.Combine(_chunksPath, $"{chunkId}.json.gz");
        await using var fileStream = File.OpenRead(chunkPath);
        await using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
        using var memory = new MemoryStream();
        await gzipStream.CopyToAsync(memory, cancellationToken);
        return memory.ToArray();
    }
}
