using DevMemory.Core.Entities;

namespace DevMemory.Core.Interfaces;

public interface ISearchService
{
    Task<IEnumerable<Observation>> SearchAsync(
        string query,
        string? project = null,
        int limit = 10,
        string? type = null,
        string[]? tags = null,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<Observation>> GetTimelineAsync(
        Guid observationId,
        int beforeCount = 3,
        int afterCount = 3,
        CancellationToken cancellationToken = default);
}
