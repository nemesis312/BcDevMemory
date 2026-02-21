using Dapper;
using DevMemory.Core.Entities;
using DevMemory.Core.Interfaces;
using DevMemory.Infrastructure.Data;

namespace DevMemory.Infrastructure.Search;

public sealed class PostgresSearchService : ISearchService
{
    private readonly DapperContext _context;

    public PostgresSearchService(DapperContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Observation>> SearchAsync(
        string query,
        string? project = null,
        int limit = 10,
        string? type = null,
        string[]? tags = null,
        CancellationToken cancellationToken = default)
    {
        const string sql =
            "SELECT * FROM sp_search_observations(@p_query, @p_project, @p_limit)";

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<SearchRow>(sql, new
        {
            p_query = query,
            p_project = project,
            p_limit = limit
        });

        return rows.Select(r => r.ToObservation());
    }

    public async Task<IEnumerable<Observation>> GetTimelineAsync(
        Guid observationId,
        int beforeCount = 3,
        int afterCount = 3,
        CancellationToken cancellationToken = default)
    {
        const string sql =
            "SELECT * FROM sp_get_timeline(@p_observation_id, @p_before, @p_after)";

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<TimelineRow>(sql, new
        {
            p_observation_id = observationId,
            p_before = beforeCount,
            p_after = afterCount,
        });

        return rows.Select(r => r.ToObservation());
    }

    // ── internal DTOs ──────────────────────────────────────────────────────

    // Classes (not records) so Dapper uses property setters and
    // type handlers fire correctly for arrays and timestamps.

    private sealed class SearchRow
    {
        public Guid Id { get; set; }
        public Guid Session_Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Content_Preview { get; set; } = string.Empty;
        public string? Project { get; set; }
        public float Rank { get; set; }
        public DateTime Created_At { get; set; }

        public Observation ToObservation() => new()
        {
            Id = Id,
            SessionId = Session_Id,
            Title = Title,
            Type = Type,
            Content = string.Empty,  // preview only
            ContentPreview = Content_Preview,
            Project = Project,
            Rank = Rank,
            CreatedAt = Created_At,
        };
    }

    private sealed class TimelineRow
    {
        public Guid Id { get; set; }
        public Guid Session_Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? Project { get; set; }
        public string[]? Tags { get; set; }
        public DateTime Created_At { get; set; }
        public string Timeline_Pos { get; set; } = string.Empty;

        public Observation ToObservation() => new()
        {
            Id = Id,
            SessionId = Session_Id,
            Title = Title,
            Type = Type,
            Content = Content,
            Project = Project,
            Tags = Tags ?? [],
            CreatedAt = Created_At,
        };
    }
}
