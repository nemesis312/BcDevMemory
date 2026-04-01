using DevMemory.Core.Models;

namespace DevMemory.Core.Interfaces;

public interface ISyncTransport
{
    Task<SyncManifest> ReadManifestAsync(CancellationToken cancellationToken = default);
    Task WriteManifestAsync(SyncManifest manifest, CancellationToken cancellationToken = default);
    Task WriteChunkAsync(string chunkId, byte[] data, CancellationToken cancellationToken = default);
    Task<byte[]> ReadChunkAsync(string chunkId, CancellationToken cancellationToken = default);
}
