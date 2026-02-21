using System.Text;
using System.Text.Json;
using Dapper;
using DevMemory.Core.Entities;
using DevMemory.Core.Interfaces;
using DevMemory.Infrastructure.Data;

namespace DevMemory.Infrastructure.Search;

public sealed class SqliteSearchService : ISearchService
{
    private readonly SqliteContext _context;

    public SqliteSearchService(SqliteContext context) => _context = context;

    public async Task<IEnumerable<Observation>> SearchAsync(
        string query,
        string? project = null,
        int limit = 10,
        string? type = null,
        string[]? tags = null,
        CancellationToken cancellationToken = default)
    {
        // Build dynamic WHERE clause for filters applied after FTS join
        var filters = new StringBuilder();
        if (project != null) filters.Append(" AND o.project = @Project");
        if (type != null)    filters.Append(" AND o.type = @Type");

        var sql = $"""
            SELECT o.id, o.session_id, o.title, o.type,
                   substr(o.content, 1, 200) AS content_preview,
                   o.project, fts.rank, o.created_at, o.tags
            FROM observations_fts fts
            JOIN observations o ON o.id = fts.observation_id
            WHERE observations_fts MATCH @Query{filters}
            ORDER BY fts.rank
            LIMIT @Limit
            """;

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var rows = (await connection.QueryAsync<FtsRow>(sql, new
        {
            Query   = query,
            Project = project,
            Type    = type,
            Limit   = limit,
        })).ToList();

        // Tags filter done in memory (JSON array in TEXT column)
        IEnumerable<FtsRow> filtered = rows;
        if (tags is { Length: > 0 })
        {
            filtered = rows.Where(r =>
            {
                if (r.Tags is null) return false;
                var rowTags = JsonSerializer.Deserialize<string[]>(r.Tags) ?? [];
                return tags.Any(t => rowTags.Contains(t, StringComparer.OrdinalIgnoreCase));
            });
        }

        return filtered.Select(r => r.ToObservation());
    }

    public async Task<IEnumerable<Observation>> GetTimelineAsync(
        Guid observationId,
        int beforeCount = 3,
        int afterCount = 3,
        CancellationToken cancellationToken = default)
    {
        // Find the anchor first
        const string anchorSql = "SELECT created_at, project FROM observations WHERE id = @Id";

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var anchor = await connection.QueryFirstOrDefaultAsync<(string created_at, string? project)>(
            anchorSql, new { Id = observationId.ToString() });

        if (anchor == default) return [];

        const string sql = """
            SELECT *, 'anchor' AS timeline_pos FROM observations WHERE id = @Id

            UNION ALL

            SELECT *, 'before' AS timeline_pos
            FROM (
                SELECT * FROM observations
                WHERE project = @Project AND created_at < @AnchorTime AND id != @Id
                ORDER BY created_at DESC LIMIT @Before
            )

            UNION ALL

            SELECT *, 'after' AS timeline_pos
            FROM (
                SELECT * FROM observations
                WHERE project = @Project AND created_at > @AnchorTime AND id != @Id
                ORDER BY created_at ASC LIMIT @After
            )

            ORDER BY created_at
            """;

        var rows = await connection.QueryAsync<TimelineRow>(sql, new
        {
            Id         = observationId.ToString(),
            Project    = anchor.project,
            AnchorTime = anchor.created_at,
            Before     = beforeCount,
            After      = afterCount,
        });

        return rows.Select(r => r.ToObservation());
    }

    // ── internal DTOs ───────────────────────────────────────────────────────

    private sealed class FtsRow
    {
        public string  Id              { get; set; } = string.Empty;
        public string  Session_Id      { get; set; } = string.Empty;
        public string  Title           { get; set; } = string.Empty;
        public string  Type            { get; set; } = string.Empty;
        public string  Content_Preview { get; set; } = string.Empty;
        public string? Project         { get; set; }
        public double  Rank            { get; set; }
        public string  Created_At      { get; set; } = string.Empty;
        public string? Tags            { get; set; }

        public Observation ToObservation() => new()
        {
            Id             = Guid.Parse(Id),
            SessionId      = Guid.Parse(Session_Id),
            Title          = Title,
            Type           = Type,
            Content        = string.Empty,
            ContentPreview = Content_Preview,
            Project        = Project,
            Rank           = (float)-Rank, // FTS5 rank is negative; negate for display
            CreatedAt      = DateTime.Parse(Created_At, null, System.Globalization.DateTimeStyles.RoundtripKind),
        };
    }

    private sealed class TimelineRow
    {
        public string  Id          { get; set; } = string.Empty;
        public string  Session_Id  { get; set; } = string.Empty;
        public string  Title       { get; set; } = string.Empty;
        public string  Type        { get; set; } = string.Empty;
        public string  Content     { get; set; } = string.Empty;
        public string? Project     { get; set; }
        public string? Tags        { get; set; }
        public string  Created_At  { get; set; } = string.Empty;

        public Observation ToObservation() => new()
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
}
