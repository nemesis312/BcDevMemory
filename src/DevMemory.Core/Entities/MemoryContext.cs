namespace DevMemory.Core.Entities;

/// <summary>
/// Aggregated context returned by mem_context — recent sessions + their observations.
/// </summary>
public class MemoryContext
{
    public string Project { get; set; } = string.Empty;
    public List<SessionContext> RecentSessions { get; set; } = new();
}

public class SessionContext
{
    public Guid SessionId { get; set; }
    public string? SessionGoal { get; set; }
    public string? SessionSummary { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public long ObservationCount { get; set; }
    public List<ObservationSummary> RecentObservations { get; set; } = new();
}

public class ObservationSummary
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
