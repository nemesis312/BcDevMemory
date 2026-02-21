using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using DevMemory.Core.Entities;
using DevMemory.Core.Interfaces;

namespace DevMemory.Infrastructure.Data;

public sealed class SqliteSessionRepository : ISessionRepository
{
    private readonly SqliteContext _context;

    public SqliteSessionRepository(SqliteContext context) => _context = context;

    public async Task<Session> CreateSessionAsync(
        string? project = null,
        string? goal = null,
        CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        var startedAt = DateTime.UtcNow;

        const string sql = """
            INSERT INTO sessions (id, project, goal, started_at)
            VALUES (@Id, @Project, @Goal, @StartedAt)
            """;

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(sql, new
        {
            Id = id.ToString(),
            Project = project,
            Goal = goal,
            StartedAt = startedAt.ToString("O"),
        });

        return new Session
        {
            Id = id,
            Project = project,
            Goal = goal,
            StartedAt = startedAt,
            IsActive = true,
        };
    }

    public async Task<Session?> GetActiveSessionAsync(
        string? project = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT * FROM sessions
            WHERE is_active = 1
              AND (@Project IS NULL OR project = @Project)
            ORDER BY started_at DESC
            LIMIT 1
            """;

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var row = await connection.QueryFirstOrDefaultAsync<SessionRow>(sql,
            new { Project = project });
        return row?.ToEntity();
    }

    public async Task<Session?> GetSessionAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM sessions WHERE id = @Id";

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var row = await connection.QueryFirstOrDefaultAsync<SessionRow>(sql,
            new { Id = id.ToString() });
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
        await connection.ExecuteAsync(sql, new
        {
            Id = id.ToString(),
            Summary = summary,
            FilesChanged = filesChanged is null ? null : JsonSerializer.Serialize(filesChanged),
        });
    }

    public async Task EndSessionAsync(
        Guid id,
        string? summary = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE sessions
            SET is_active = 0,
                ended_at  = @EndedAt,
                summary   = COALESCE(@Summary, summary)
            WHERE id = @Id
            """;

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(sql, new
        {
            Id = id.ToString(),
            EndedAt = DateTime.UtcNow.ToString("O"),
            Summary = summary,
        });
    }

    public async Task<IEnumerable<Session>> GetRecentSessionsAsync(
        string? project = null,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT * FROM sessions
            WHERE @Project IS NULL OR project = @Project
            ORDER BY started_at DESC
            LIMIT @Limit
            """;

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<SessionRow>(sql,
            new { Project = project, Limit = limit });
        return rows.Select(r => r.ToEntity());
    }

    public async Task<MemoryContext> GetProjectContextAsync(
        string project,
        int sessionLimit = 5,
        CancellationToken cancellationToken = default)
    {
        const string sessionsSql = """
            SELECT * FROM sessions
            WHERE project = @Project AND is_active = 0
            ORDER BY started_at DESC
            LIMIT @Limit
            """;

        const string obsSql = """
            SELECT id, title, type, created_at FROM observations
            WHERE session_id = @SessionId
            ORDER BY created_at DESC
            LIMIT 5
            """;

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var sessionRows = (await connection.QueryAsync<SessionRow>(sessionsSql,
            new { Project = project, Limit = sessionLimit })).ToList();

        var contexts = new List<SessionContext>();
        foreach (var sr in sessionRows)
        {
            var obsSummaries = await connection.QueryAsync<ObsSummaryRow>(obsSql,
                new { SessionId = sr.Id });

            var ctx = new SessionContext
            {
                SessionId = Guid.Parse(sr.Id),
                SessionGoal = sr.Goal,
                SessionSummary = sr.Summary,
                StartedAt = Parse(sr.Started_At),
                EndedAt = sr.Ended_At is null ? null : Parse(sr.Ended_At),
                ObservationCount = 0, // filled below
                RecentObservations = obsSummaries.Select(o => new ObservationSummary
                {
                    Id = Guid.Parse(o.Id),
                    Title = o.Title,
                    Type = o.Type,
                    CreatedAt = Parse(o.Created_At),
                }).ToList(),
            };

            // quick count
            ctx.ObservationCount = ctx.RecentObservations.Count;
            contexts.Add(ctx);
        }

        return new MemoryContext { Project = project, RecentSessions = contexts };
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private static DateTime Parse(string s) =>
        DateTime.Parse(s, null, System.Globalization.DateTimeStyles.RoundtripKind);

    // ── internal DTOs ───────────────────────────────────────────────────────

    private sealed class SessionRow
    {
        public string Id { get; set; } = string.Empty;
        public string? Project { get; set; }
        public string? Goal { get; set; }
        public string? Summary { get; set; }
        public string? Files_Changed { get; set; }
        public string Started_At { get; set; } = string.Empty;
        public string? Ended_At { get; set; }
        public int Is_Active { get; set; }

        public Session ToEntity() => new()
        {
            Id = Guid.Parse(Id),
            Project = Project,
            Goal = Goal,
            Summary = Summary,
            FilesChanged = Files_Changed is null ? [] : JsonSerializer.Deserialize<string[]>(Files_Changed) ?? [],
            StartedAt = Parse(Started_At),
            EndedAt = Ended_At is null ? null : Parse(Ended_At),
            IsActive = Is_Active == 1,
        };
    }

    private sealed class ObsSummaryRow
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Created_At { get; set; } = string.Empty;
    }

    public async Task<int> CloseStaleSessionsAsync(
        TimeSpan staleThreshold,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.Subtract(staleThreshold).ToString("O");

        const string sql = """
            UPDATE sessions
            SET is_active = 0,
                ended_at  = @Now,
                summary   = COALESCE(summary, '[Auto-closed: stale session]')
            WHERE is_active = 1
              AND started_at < @Cutoff
            """;

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        return await connection.ExecuteAsync(sql, new
        {
            Cutoff = cutoff,
            Now = DateTime.UtcNow.ToString("O"),
        });
    }

    public async Task<Session?> GetStaleActiveSessionAsync(
        string? project,
        TimeSpan staleThreshold,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.Subtract(staleThreshold).ToString("O");

        const string sql = """
            SELECT * FROM sessions
            WHERE is_active = 1
              AND (@Project IS NULL OR project = @Project)
              AND started_at < @Cutoff
            ORDER BY started_at DESC
            LIMIT 1
            """;

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var row = await connection.QueryFirstOrDefaultAsync<SessionRow>(sql,
            new { Project = project, Cutoff = cutoff });
        return row?.ToEntity();
    }
}
