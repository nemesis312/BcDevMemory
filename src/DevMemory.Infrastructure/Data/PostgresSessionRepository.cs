using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using DevMemory.Core.Entities;
using DevMemory.Core.Interfaces;

namespace DevMemory.Infrastructure.Data;

public sealed class PostgresSessionRepository : ISessionRepository
{
    private readonly DapperContext _context;

    public PostgresSessionRepository(DapperContext context)
    {
        _context = context;
    }

    public async Task<Session> CreateSessionAsync(
        string? project = null,
        string? goal = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO sessions (project, goal)
            VALUES (@Project, @Goal)
            RETURNING *
            """;

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var row = await connection.QueryFirstAsync<SessionRow>(sql, new { Project = project, Goal = goal });
        return row.ToEntity();
    }

    public async Task<Session?> GetActiveSessionAsync(
        string? project = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT * FROM sessions
            WHERE is_active = TRUE
              AND (@Project::VARCHAR IS NULL OR project = @Project)
            ORDER BY started_at DESC
            LIMIT 1
            """;

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var row = await connection.QueryFirstOrDefaultAsync<SessionRow>(sql, new { Project = project });
        return row?.ToEntity();
    }

    public async Task<Session?> GetSessionAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM sessions WHERE id = @Id";

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var row = await connection.QueryFirstOrDefaultAsync<SessionRow>(sql, new { Id = id });
        return row?.ToEntity();
    }

    public async Task UpdateSessionAsync(
        Guid id,
        string? summary = null,
        string[]? filesChanged = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE sessions
            SET summary       = COALESCE(@Summary, summary),
                files_changed = COALESCE(@FilesChanged, files_changed)
            WHERE id = @Id
            """;

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("Id", id);
        parameters.Add("Summary", summary);
        AddStringArray(parameters, "FilesChanged", filesChanged);

        await connection.ExecuteAsync(sql, parameters);
    }

    public async Task EndSessionAsync(
        Guid id,
        string? summary = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE sessions
            SET is_active = FALSE,
                ended_at  = NOW(),
                summary   = COALESCE(@Summary, summary)
            WHERE id = @Id
            """;

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(sql, new { Id = id, Summary = summary });
    }

    public async Task<IEnumerable<Session>> GetRecentSessionsAsync(
        string? project = null,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT * FROM sessions
            WHERE @Project::VARCHAR IS NULL OR project = @Project
            ORDER BY started_at DESC
            LIMIT @Limit
            """;

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<SessionRow>(sql, new { Project = project, Limit = limit });
        return rows.Select(r => r.ToEntity());
    }

    public async Task<MemoryContext> GetProjectContextAsync(
        string project,
        int sessionLimit = 5,
        CancellationToken cancellationToken = default)
    {
        // Cast JSONB → text so Dapper maps it as a plain string.
        const string sql = """
            SELECT session_id,
                   session_goal,
                   session_summary,
                   started_at,
                   ended_at,
                   observation_count,
                   recent_observations::text AS recent_observations_json
            FROM sp_get_session_context(@p_project, @p_limit)
            """;

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<SessionContextRow>(
            sql, new { p_project = project, p_limit = sessionLimit });

        return new MemoryContext
        {
            Project = project,
            RecentSessions = rows.Select(r => r.ToSessionContext()).ToList(),
        };
    }

    // ── helpers ────────────────────────────────────────────────────────────

    private static void AddStringArray(DynamicParameters p, string name, string[]? value)
    {
        if (value is null)
            p.Add(name, null);
        else
            p.Add(name, value, System.Data.DbType.Object);
    }

    // Class (not record) so Dapper uses property setters and
    // StringArrayTypeHandler fires for Files_Changed column.
    private sealed class SessionRow
    {
        public Guid Id { get; set; }
        public string? Project { get; set; }
        public string? Goal { get; set; }
        public string? Summary { get; set; }
        public string[]? Files_Changed { get; set; }
        public DateTime Started_At { get; set; }
        public DateTime? Ended_At { get; set; }
        public bool Is_Active { get; set; }

        public Session ToEntity() => new()
        {
            Id = Id,
            Project = Project,
            Goal = Goal,
            Summary = Summary,
            FilesChanged = Files_Changed ?? [],
            StartedAt = Started_At,
            EndedAt = Ended_At,
            IsActive = Is_Active,
        };
    }

    private sealed class SessionContextRow
    {
        public Guid Session_Id { get; set; }
        public string? Session_Goal { get; set; }
        public string? Session_Summary { get; set; }
        public DateTime Started_At { get; set; }
        public DateTime? Ended_At { get; set; }
        public long Observation_Count { get; set; }
        public string? Recent_Observations_Json { get; set; }

        public SessionContext ToSessionContext()
        {
            var ctx = new SessionContext
            {
                SessionId = Session_Id,
                SessionGoal = Session_Goal,
                SessionSummary = Session_Summary,
                StartedAt = Started_At,
                EndedAt = Ended_At,
                ObservationCount = Observation_Count,
            };

            if (!string.IsNullOrEmpty(Recent_Observations_Json))
            {
                var summaries = JsonSerializer.Deserialize<List<JsonObsSummary>>(
                    Recent_Observations_Json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (summaries != null)
                    ctx.RecentObservations = summaries
                        .Select(s => new ObservationSummary
                        {
                            Id = s.Id,
                            Title = s.Title,
                            Type = s.Type,
                            CreatedAt = s.Created_At,
                        })
                        .ToList();
            }

            return ctx;
        }

        // Mirrors the JSONB structure from sp_get_session_context
        private sealed class JsonObsSummary
        {
            [JsonPropertyName("id")] public Guid Id { get; set; }
            [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
            [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
            [JsonPropertyName("created_at")] public DateTime Created_At { get; set; }
        }
    }

    public async Task<int> CloseStaleSessionsAsync(
        TimeSpan staleThreshold,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE sessions
            SET is_active = FALSE,
                ended_at  = NOW(),
                summary   = COALESCE(summary, '[Auto-closed: stale session]')
            WHERE is_active = TRUE
              AND started_at < NOW() - @Threshold::INTERVAL
            """;

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        // PostgreSQL interval format: '24 hours', '1 day', etc.
        var intervalStr = $"{staleThreshold.TotalHours} hours";
        return await connection.ExecuteAsync(sql, new { Threshold = intervalStr });
    }

    public async Task<Session?> GetStaleActiveSessionAsync(
        string? project,
        TimeSpan staleThreshold,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT * FROM sessions
            WHERE is_active = TRUE
              AND (@Project::VARCHAR IS NULL OR project = @Project)
              AND started_at < NOW() - @Threshold::INTERVAL
            ORDER BY started_at DESC
            LIMIT 1
            """;

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var intervalStr = $"{staleThreshold.TotalHours} hours";
        var row = await connection.QueryFirstOrDefaultAsync<SessionRow>(sql,
            new { Project = project, Threshold = intervalStr });
        return row?.ToEntity();
    }
}
