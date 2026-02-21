using DevMemory.Core.Entities;

namespace DevMemory.Core.Interfaces;

/// <summary>
/// Graph-specific operations available only when using a graph database (Neo4j).
/// Enables linking observations and traversing relationship networks.
/// </summary>
public interface IGraphRepository
{
    /// <summary>
    /// Creates a RELATES_TO relationship between two observations.
    /// </summary>
    Task LinkObservationsAsync(
        Guid fromId,
        Guid toId,
        string? reason = null,
        float strength = 1.0f,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Traverses RELATES_TO relationships transitively to find related observations.
    /// </summary>
    Task<IEnumerable<Observation>> GetRelatedAsync(
        Guid observationId,
        int depth = 2,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Follows RELATES_TO edges backward to trace the history behind a decision.
    /// </summary>
    Task<IEnumerable<Observation>> GetDecisionChainAsync(
        Guid decisionId,
        CancellationToken cancellationToken = default);
}
