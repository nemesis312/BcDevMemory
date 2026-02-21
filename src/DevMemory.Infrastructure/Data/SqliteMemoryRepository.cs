using System.Text.Json;
using Dapper;
using DevMemory.Core.Entities;
using DevMemory.Core.Interfaces;
using DevMemory.Core.Utilities;

namespace DevMemory.Infrastructure.Data;

public sealed class SqliteMemoryRepository : IMemoryRepository
{
    private readonly SqliteContext _context;

    public SqliteMemoryRepository(SqliteContext context) => _context = context;

    public async Task<Observation> SaveObservationAsync(
        Guid sessionId,
        string title,
        string type,
        string content,
        string? project = null,
        string[]? tags = null,
        CancellationToken cancellationToken = default)
    {
        content = PrivacyHelper.StripPrivateTags(content);

        var id        = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        var tagsJson  = JsonSerializer.Serialize(tags ?? []);

        const string sql = """
            INSERT INTO observations (id, session_id, title, type, content, project, tags, created_at)
            VALUES (@Id, @SessionId, @Title, @Type, @Content, @Project, @Tags, @CreatedAt)
            """;

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(sql, new
        {
            Id        = id.ToString(),
            SessionId = sessionId.ToString(),
            Title     = title,
            Type      = type,
            Content   = content,
            Project   = project,
            Tags      = tagsJson,
            CreatedAt = createdAt.ToString("O"),
        });

        return new Observation
        {
            Id        = id,
            SessionId = sessionId,
            Title     = title,
            Type      = type,
            Content   = content,
            Project   = project,
            Tags      = tags ?? [],
            CreatedAt = createdAt,
        };
    }

    public async Task<Observation?> GetObservationAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM observations WHERE id = @Id";

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var row = await connection.QueryFirstOrDefaultAsync<ObsRow>(sql,
            new { Id = id.ToString() });
        return row?.ToEntity();
    }

    public async Task<IEnumerable<Observation>> GetObservationsBySessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT * FROM observations
            WHERE session_id = @SessionId
            ORDER BY created_at DESC
            """;

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<ObsRow>(sql,
            new { SessionId = sessionId.ToString() });
        return rows.Select(r => r.ToEntity());
    }

    public async Task<IEnumerable<Observation>> GetRecentObservationsAsync(
        string? project = null,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT * FROM observations
            WHERE @Project IS NULL OR project = @Project
            ORDER BY created_at DESC
            LIMIT @Limit
            """;

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<ObsRow>(sql,
            new { Project = project, Limit = limit });
        return rows.Select(r => r.ToEntity());
    }

    public async Task SavePromptAsync(
        Guid sessionId,
        string content,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO prompts (id, session_id, content, created_at)
            VALUES (@Id, @SessionId, @Content, @CreatedAt)
            """;

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(sql, new
        {
            Id        = Guid.NewGuid().ToString(),
            SessionId = sessionId.ToString(),
            Content   = content,
            CreatedAt = DateTime.UtcNow.ToString("O"),
        });
    }

    public async Task<MemoryStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                (SELECT COUNT(*) FROM observations)                               AS total_observations,
                (SELECT COUNT(*) FROM sessions)                                   AS total_sessions,
                (SELECT COUNT(*) FROM sessions WHERE is_active = 1)              AS active_sessions,
                (SELECT COUNT(DISTINCT project) FROM observations WHERE project IS NOT NULL) AS projects,
                (SELECT MIN(created_at) FROM observations)                        AS oldest_observation,
                (SELECT MAX(created_at) FROM observations)                        AS newest_observation
            """;

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var row = await connection.QueryFirstAsync<StatsRow>(sql);
        return row.ToStats();
    }

    // ── internal DTOs ───────────────────────────────────────────────────────

    private sealed class ObsRow
    {
        public string  Id         { get; set; } = string.Empty;
        public string  Session_Id { get; set; } = string.Empty;
        public string  Title      { get; set; } = string.Empty;
        public string  Type       { get; set; } = string.Empty;
        public string  Content    { get; set; } = string.Empty;
        public string? Project    { get; set; }
        public string? Tags       { get; set; }
        public string  Created_At { get; set; } = string.Empty;

        public Observation ToEntity() => new()
        {
            Id        = Guid.Parse(Id),
            SessionId = Guid.Parse(Session_Id),
            Title     = Title,
            Type      = Type,
            Content   = Content,
            Project   = Project,
            Tags      = Tags is null ? [] : JsonSerializer.Deserialize<string[]>(Tags) ?? [],
            CreatedAt = DateTime.Parse(Created_At, null, System.Globalization.DateTimeStyles.RoundtripKind),
        };
    }

    private sealed class StatsRow
    {
        public long    Total_Observations { get; set; }
        public long    Total_Sessions     { get; set; }
        public long    Active_Sessions    { get; set; }
        public long    Projects           { get; set; }
        public string? Oldest_Observation { get; set; }
        public string? Newest_Observation { get; set; }

        public MemoryStats ToStats() => new()
        {
            TotalObservations = Total_Observations,
            TotalSessions     = Total_Sessions,
            ActiveSessions    = Active_Sessions,
            Projects          = Projects,
            OldestObservation = Oldest_Observation is null ? null
                : DateTime.Parse(Oldest_Observation, null, System.Globalization.DateTimeStyles.RoundtripKind),
            NewestObservation = Newest_Observation is null ? null
                : DateTime.Parse(Newest_Observation, null, System.Globalization.DateTimeStyles.RoundtripKind),
        };
    }
}
