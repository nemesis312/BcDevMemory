using DevMemory.Core.Entities;

namespace DevMemory.Core.Interfaces;

public interface IRunStateRepository
{
    Task<RunState> SaveCheckpointAsync(
        string runId,
        string projectName,
        string goal,
        string currentPhase,
        Dictionary<string, bool> approvals,
        string? lastHandoffSummary = null,
        string[]? openRisks = null,
        string mode = "safe",
        CancellationToken cancellationToken = default);

    Task<RunState?> GetRunStateAsync(
        string runId,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<RunState>> ListActiveRunsAsync(
        string projectName,
        CancellationToken cancellationToken = default);
}
