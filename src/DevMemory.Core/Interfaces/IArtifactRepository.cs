using DevMemory.Core.Entities;

namespace DevMemory.Core.Interfaces;

public interface IArtifactRepository
{
    Task<Artifact> SaveArtifactAsync(
        string runId,
        string projectName,
        string type,
        string content,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    Task<Artifact?> GetLatestArtifactAsync(
        string runId,
        string type,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<Artifact>> ListRunArtifactsAsync(
        string runId,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<Artifact>> ListProjectArtifactsAsync(
        string projectName,
        CancellationToken cancellationToken = default);
}
