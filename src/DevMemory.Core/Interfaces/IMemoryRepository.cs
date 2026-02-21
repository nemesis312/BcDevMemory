using DevMemory.Core.Entities;

namespace DevMemory.Core.Interfaces;

public interface IMemoryRepository
{
    Task<Observation> SaveObservationAsync(
        Guid sessionId,
        string title,
        string type,
        string content,
        string? project = null,
        string[]? tags = null,
        CancellationToken cancellationToken = default);

    Task<Observation?> GetObservationAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<Observation>> GetObservationsBySessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<Observation>> GetRecentObservationsAsync(
        string? project = null,
        int limit = 20,
        CancellationToken cancellationToken = default);

    Task SavePromptAsync(
        Guid sessionId,
        string content,
        CancellationToken cancellationToken = default);

    Task<MemoryStats> GetStatsAsync(
        CancellationToken cancellationToken = default);
}
