using DevMemory.Core.Entities;

namespace DevMemory.Core.Interfaces;

public interface ISessionRepository
{
    Task<Session> CreateSessionAsync(
        string? project = null,
        string? goal = null,
        CancellationToken cancellationToken = default);

    Task<Session?> GetActiveSessionAsync(
        string? project = null,
        CancellationToken cancellationToken = default);

    Task<Session?> GetSessionAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task UpdateSessionAsync(
        Guid id,
        string? summary = null,
        string[]? filesChanged = null,
        CancellationToken cancellationToken = default);

    Task EndSessionAsync(
        Guid id,
        string? summary = null,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<Session>> GetRecentSessionsAsync(
        string? project = null,
        int limit = 5,
        CancellationToken cancellationToken = default);

    Task<MemoryContext> GetProjectContextAsync(
        string project,
        int sessionLimit = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes sessions that have been active longer than the specified threshold.
    /// Returns the number of sessions closed.
    /// </summary>
    Task<int> CloseStaleSessionsAsync(
        TimeSpan staleThreshold,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active session for a project if it's older than the threshold (stale).
    /// Used for detection/warning without auto-closing.
    /// </summary>
    Task<Session?> GetStaleActiveSessionAsync(
        string? project,
        TimeSpan staleThreshold,
        CancellationToken cancellationToken = default);
}
