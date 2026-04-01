using DevMemory.Core.Models;

namespace DevMemory.Core.Interfaces;

public interface ISyncService
{
    Task<SyncInitResult> InitializeAsync(SyncOptions options, CancellationToken cancellationToken = default);
    Task<SyncStatus> GetStatusAsync(SyncOptions options, CancellationToken cancellationToken = default);
    Task<SyncExportResult> ExportAsync(SyncOptions options, CancellationToken cancellationToken = default);
    Task<SyncImportResult> ImportAsync(SyncOptions options, CancellationToken cancellationToken = default);
}
