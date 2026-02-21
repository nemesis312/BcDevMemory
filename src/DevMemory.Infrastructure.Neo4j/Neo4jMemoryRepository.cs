using DevMemory.Core.Entities;
using DevMemory.Core.Interfaces;
using DevMemory.Core.Utilities;
using Neo4j.Driver;

namespace DevMemory.Infrastructure.Neo4j;

/// <summary>
/// Implements IMemoryRepository and IGraphRepository using Neo4j.
/// All observations are stored as (:Observation) nodes connected to (:Session) nodes.
/// </summary>
public sealed class Neo4jMemoryRepository : IMemoryRepository, IGraphRepository
{
    private readonly Neo4jContext _context;

    public Neo4jMemoryRepository(Neo4jContext context)
    {
        _context = context;
    }

    // ── IMemoryRepository ─────────────────────────────────────────────────────

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
        var id      = Guid.NewGuid().ToString();
        var sidStr  = sessionId.ToString();
        var tagList = tags ?? [];

        await using var session = _context.Session();

        var node = await session.ExecuteWriteAsync(async tx =>
        {
            const string query = """
                MATCH (s:Session {id: $sessionId})
                CREATE (o:Observation {
                    id:        $id,
                    sessionId: $sessionId,
                    title:     $title,
                    type:      $type,
                    content:   $content,
                    project:   $project,
                    tags:      $tags,
                    createdAt: datetime()
                })
                CREATE (s)-[:CONTAINS]->(o)
                WITH o
                UNWIND CASE WHEN size($tags) = 0 THEN [null] ELSE $tags END AS tagName
                FOREACH (_ IN CASE WHEN tagName IS NOT NULL THEN [1] ELSE [] END |
                    MERGE (t:Tag {name: tagName})
                    MERGE (o)-[:TAGGED_WITH]->(t)
                )
                RETURN o
                """;

            var parameters = new Dictionary<string, object?>
            {
                ["sessionId"] = sidStr,
                ["id"]        = id,
                ["title"]     = title,
                ["type"]      = type,
                ["content"]   = content,
                ["project"]   = (object?)project,
                ["tags"]      = tagList,
            };

            var cursor = await tx.RunAsync(query, parameters);
            var record = await cursor.SingleAsync();
            return record["o"].As<INode>();
        });

        return MapNode(node);
    }

    public async Task<Observation?> GetObservationAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using var session = _context.Session();

        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MATCH (o:Observation {id: $id}) RETURN o",
                new Dictionary<string, object?> { ["id"] = id.ToString() });

            if (!await cursor.FetchAsync())
                return null;

            return MapNode(cursor.Current["o"].As<INode>());
        });
    }

    public async Task<IEnumerable<Observation>> GetObservationsBySessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        await using var session = _context.Session();

        return await session.ExecuteReadAsync(async tx =>
        {
            const string query = """
                MATCH (s:Session {id: $sessionId})-[:CONTAINS]->(o:Observation)
                RETURN o
                ORDER BY o.createdAt DESC
                """;

            var cursor = await tx.RunAsync(query,
                new Dictionary<string, object?> { ["sessionId"] = sessionId.ToString() });
            var records = await cursor.ToListAsync();
            return records.Select(r => MapNode(r["o"].As<INode>())).ToList();
        });
    }

    public async Task<IEnumerable<Observation>> GetRecentObservationsAsync(
        string? project = null,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        await using var session = _context.Session();

        return await session.ExecuteReadAsync(async tx =>
        {
            const string query = """
                MATCH (o:Observation)
                WHERE $project IS NULL OR o.project = $project
                RETURN o
                ORDER BY o.createdAt DESC
                LIMIT $limit
                """;

            var cursor = await tx.RunAsync(query,
                new Dictionary<string, object?> { ["project"] = (object?)project, ["limit"] = limit });
            var records = await cursor.ToListAsync();
            return records.Select(r => MapNode(r["o"].As<INode>())).ToList();
        });
    }

    public async Task SavePromptAsync(
        Guid sessionId,
        string content,
        CancellationToken cancellationToken = default)
    {
        await using var session = _context.Session();

        await session.ExecuteWriteAsync(async tx =>
        {
            const string query = """
                MATCH (s:Session {id: $sessionId})
                CREATE (p:Prompt {
                    id:        $id,
                    sessionId: $sessionId,
                    content:   $content,
                    createdAt: datetime()
                })
                CREATE (s)-[:HAS_PROMPT]->(p)
                """;

            await tx.RunAsync(query, new Dictionary<string, object?>
            {
                ["sessionId"] = sessionId.ToString(),
                ["id"]        = Guid.NewGuid().ToString(),
                ["content"]   = content,
            });
            return 0;
        });
    }

    public async Task<MemoryStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        await using var session = _context.Session();

        return await session.ExecuteReadAsync(async tx =>
        {
            var obsCursor = await tx.RunAsync(
                "MATCH (o:Observation) RETURN count(o) AS total, min(o.createdAt) AS oldest, max(o.createdAt) AS newest");
            var obsRecord = await obsCursor.SingleAsync();

            var sesCursor = await tx.RunAsync(
                "MATCH (s:Session) RETURN count(s) AS total");
            var sesRecord = await sesCursor.SingleAsync();

            var activeCursor = await tx.RunAsync(
                "MATCH (s:Session {isActive: true}) RETURN count(s) AS total");
            var activeRecord = await activeCursor.SingleAsync();

            var projCursor = await tx.RunAsync(
                "MATCH (p:Project) RETURN count(p) AS total");
            var projRecord = await projCursor.SingleAsync();

            return new MemoryStats
            {
                TotalObservations = obsRecord["total"].As<long>(),
                OldestObservation = NullableZdtToDateTime(obsRecord["oldest"]),
                NewestObservation = NullableZdtToDateTime(obsRecord["newest"]),
                TotalSessions     = sesRecord["total"].As<long>(),
                ActiveSessions    = activeRecord["total"].As<long>(),
                Projects          = projRecord["total"].As<long>(),
            };
        });
    }

    // ── IGraphRepository ──────────────────────────────────────────────────────

    public async Task LinkObservationsAsync(
        Guid fromId,
        Guid toId,
        string? reason = null,
        float strength = 1.0f,
        CancellationToken cancellationToken = default)
    {
        await using var session = _context.Session();

        await session.ExecuteWriteAsync(async tx =>
        {
            const string query = """
                MATCH (a:Observation {id: $fromId})
                MATCH (b:Observation {id: $toId})
                MERGE (a)-[r:RELATES_TO]->(b)
                SET r.reason   = $reason,
                    r.strength = $strength
                """;

            await tx.RunAsync(query, new Dictionary<string, object?>
            {
                ["fromId"]   = fromId.ToString(),
                ["toId"]     = toId.ToString(),
                ["reason"]   = (object?)reason,
                ["strength"] = (double)strength,
            });
            return 0;
        });
    }

    public async Task<IEnumerable<Observation>> GetRelatedAsync(
        Guid observationId,
        int depth = 2,
        CancellationToken cancellationToken = default)
    {
        await using var session = _context.Session();

        return await session.ExecuteReadAsync(async tx =>
        {
            // Depth is clamped and injected as a literal — safe because it's an int, not user text.
            var d     = Math.Max(1, Math.Min(depth, 5));
            var query = $$"""
                MATCH (start:Observation {id: $id})
                MATCH path = (start)-[:RELATES_TO*1..{{d}}]-(related:Observation)
                WHERE related.id <> $id
                RETURN DISTINCT related, length(path) AS distance
                ORDER BY distance
                LIMIT 20
                """;

            var cursor = await tx.RunAsync(query,
                new Dictionary<string, object?> { ["id"] = observationId.ToString() });
            var records = await cursor.ToListAsync();
            return records.Select(r => MapNode(r["related"].As<INode>())).ToList();
        });
    }

    public async Task<IEnumerable<Observation>> GetDecisionChainAsync(
        Guid decisionId,
        CancellationToken cancellationToken = default)
    {
        await using var session = _context.Session();

        return await session.ExecuteReadAsync(async tx =>
        {
            const string query = """
                MATCH (decision:Observation {id: $id, type: 'decision'})
                MATCH path = (decision)<-[:RELATES_TO*]-(predecessor:Observation)
                RETURN predecessor, length(path) AS depth
                ORDER BY depth
                """;

            var cursor = await tx.RunAsync(query,
                new Dictionary<string, object?> { ["id"] = decisionId.ToString() });
            var records = await cursor.ToListAsync();
            return records.Select(r => MapNode(r["predecessor"].As<INode>())).ToList();
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Observation MapNode(INode node)
    {
        var tagsRaw = node.Properties.TryGetValue("tags", out var t) && t is not null
            ? t.As<List<object>>().Select(x => x.ToString() ?? string.Empty).ToArray()
            : Array.Empty<string>();

        return new Observation
        {
            Id        = Guid.Parse(node["id"].As<string>()),
            SessionId = Guid.Parse(node["sessionId"].As<string>()),
            Title     = node["title"].As<string>(),
            Type      = node["type"].As<string>(),
            Content   = node["content"].As<string>(),
            Project   = node.Properties.TryGetValue("project", out var p) ? p?.As<string>() : null,
            Tags      = tagsRaw,
            CreatedAt = ZdtToDateTime(node["createdAt"].As<ZonedDateTime>()),
        };
    }

    internal static DateTime ZdtToDateTime(ZonedDateTime zdt)
    {
        var offset      = TimeSpan.FromSeconds(zdt.OffsetSeconds);
        var millisecond = (int)(zdt.Nanosecond / 1_000_000);
        return new DateTimeOffset(
            zdt.Year, zdt.Month, zdt.Day,
            zdt.Hour, zdt.Minute, zdt.Second, millisecond,
            offset).UtcDateTime;
    }

    private static DateTime? NullableZdtToDateTime(object? value)
    {
        return value is ZonedDateTime zdt ? ZdtToDateTime(zdt) : null;
    }
}
