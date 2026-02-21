using DevMemory.Core.Entities;
using DevMemory.Core.Interfaces;
using Neo4j.Driver;

namespace DevMemory.Infrastructure.Neo4j;

/// <summary>
/// Implements ISessionRepository using Neo4j.
/// Sessions are (:Session) nodes; each project optionally has a (:Project) node.
/// </summary>
public sealed class Neo4jSessionRepository : ISessionRepository
{
    private readonly Neo4jContext _context;

    public Neo4jSessionRepository(Neo4jContext context)
    {
        _context = context;
    }

    public async Task<Session> CreateSessionAsync(
        string? project = null,
        string? goal = null,
        CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid().ToString();
        await using var session = _context.Session();

        var node = await session.ExecuteWriteAsync(async tx =>
        {
            const string query = """
                CREATE (s:Session {
                    id:           $id,
                    project:      $project,
                    goal:         $goal,
                    filesChanged: [],
                    startedAt:    datetime(),
                    isActive:     true
                })
                WITH s
                FOREACH (_ IN CASE WHEN $project IS NOT NULL THEN [1] ELSE [] END |
                    MERGE (p:Project {name: $project})
                    MERGE (s)-[:BELONGS_TO]->(p)
                )
                RETURN s
                """;

            var cursor = await tx.RunAsync(query, new Dictionary<string, object?>
            {
                ["id"]      = id,
                ["project"] = (object?)project,
                ["goal"]    = (object?)goal,
            });
            var record = await cursor.SingleAsync();
            return record["s"].As<INode>();
        });

        return MapNode(node);
    }

    public async Task<Session?> GetActiveSessionAsync(
        string? project = null,
        CancellationToken cancellationToken = default)
    {
        await using var session = _context.Session();

        return await session.ExecuteReadAsync(async tx =>
        {
            const string query = """
                MATCH (s:Session {isActive: true})
                WHERE $project IS NULL OR s.project = $project
                RETURN s
                ORDER BY s.startedAt DESC
                LIMIT 1
                """;

            var cursor = await tx.RunAsync(query,
                new Dictionary<string, object?> { ["project"] = (object?)project });

            if (!await cursor.FetchAsync())
                return null;

            return MapNode(cursor.Current["s"].As<INode>());
        });
    }

    public async Task<Session?> GetSessionAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using var session = _context.Session();

        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MATCH (s:Session {id: $id}) RETURN s",
                new Dictionary<string, object?> { ["id"] = id.ToString() });

            if (!await cursor.FetchAsync())
                return null;

            return MapNode(cursor.Current["s"].As<INode>());
        });
    }

    public async Task UpdateSessionAsync(
        Guid id,
        string? summary = null,
        string[]? filesChanged = null,
        CancellationToken cancellationToken = default)
    {
        await using var session = _context.Session();

        await session.ExecuteWriteAsync(async tx =>
        {
            const string query = """
                MATCH (s:Session {id: $id})
                SET s.summary      = CASE WHEN $summary      IS NOT NULL THEN $summary      ELSE s.summary      END,
                    s.filesChanged = CASE WHEN $filesChanged IS NOT NULL THEN $filesChanged ELSE s.filesChanged END
                """;

            await tx.RunAsync(query, new Dictionary<string, object?>
            {
                ["id"]           = id.ToString(),
                ["summary"]      = (object?)summary,
                ["filesChanged"] = filesChanged != null ? (object)filesChanged : null,
            });
            return 0;
        });
    }

    public async Task EndSessionAsync(
        Guid id,
        string? summary = null,
        CancellationToken cancellationToken = default)
    {
        await using var session = _context.Session();

        await session.ExecuteWriteAsync(async tx =>
        {
            const string query = """
                MATCH (s:Session {id: $id})
                SET s.isActive = false,
                    s.endedAt  = datetime(),
                    s.summary  = CASE WHEN $summary IS NOT NULL THEN $summary ELSE s.summary END
                """;

            await tx.RunAsync(query, new Dictionary<string, object?>
            {
                ["id"]      = id.ToString(),
                ["summary"] = (object?)summary,
            });
            return 0;
        });
    }

    public async Task<IEnumerable<Session>> GetRecentSessionsAsync(
        string? project = null,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        await using var session = _context.Session();

        return await session.ExecuteReadAsync(async tx =>
        {
            const string query = """
                MATCH (s:Session)
                WHERE $project IS NULL OR s.project = $project
                RETURN s
                ORDER BY s.startedAt DESC
                LIMIT $limit
                """;

            var cursor = await tx.RunAsync(query,
                new Dictionary<string, object?> { ["project"] = (object?)project, ["limit"] = limit });
            var records = await cursor.ToListAsync();
            return records.Select(r => MapNode(r["s"].As<INode>())).ToList();
        });
    }

    public async Task<MemoryContext> GetProjectContextAsync(
        string project,
        int sessionLimit = 5,
        CancellationToken cancellationToken = default)
    {
        await using var session = _context.Session();

        var sessionContexts = await session.ExecuteReadAsync(async tx =>
        {
            const string query = """
                MATCH (s:Session {project: $project, isActive: false})
                OPTIONAL MATCH (s)-[:CONTAINS]->(o:Observation)
                WITH s,
                     count(o) AS observationCount,
                     collect({id: o.id, title: o.title, type: o.type, createdAt: o.createdAt}) AS allObs
                RETURN s, observationCount, allObs
                ORDER BY s.startedAt DESC
                LIMIT $limit
                """;

            var cursor = await tx.RunAsync(query,
                new Dictionary<string, object?> { ["project"] = project, ["limit"] = sessionLimit });
            var records = await cursor.ToListAsync();

            return records.Select(r =>
            {
                var sNode = r["s"].As<INode>();
                var count = r["observationCount"].As<long>();
                var obsList = r["allObs"].As<List<IDictionary<string, object>>>();

                var recentObs = obsList
                    .Where(d => d.TryGetValue("id", out var idVal) && idVal is not null)
                    .OrderByDescending(d => d.TryGetValue("createdAt", out var ca) && ca is ZonedDateTime zdt
                        ? Neo4jMemoryRepository.ZdtToDateTime(zdt)
                        : DateTime.MinValue)
                    .Take(5)
                    .Select(d =>
                    {
                        var zdtRaw = d.TryGetValue("createdAt", out var ca) ? ca : null;
                        return new ObservationSummary
                        {
                            Id        = Guid.Parse(d["id"].ToString()!),
                            Title     = d.TryGetValue("title", out var t) ? t?.ToString() ?? string.Empty : string.Empty,
                            Type      = d.TryGetValue("type",  out var ty) ? ty?.ToString() ?? string.Empty : string.Empty,
                            CreatedAt = zdtRaw is ZonedDateTime z
                                ? Neo4jMemoryRepository.ZdtToDateTime(z)
                                : DateTime.UtcNow,
                        };
                    })
                    .ToList();

                return new SessionContext
                {
                    SessionId          = Guid.Parse(sNode["id"].As<string>()),
                    SessionGoal        = sNode.Properties.TryGetValue("goal",    out var g)  ? g?.As<string>() : null,
                    SessionSummary     = sNode.Properties.TryGetValue("summary", out var su) ? su?.As<string>() : null,
                    StartedAt          = Neo4jMemoryRepository.ZdtToDateTime(sNode["startedAt"].As<ZonedDateTime>()),
                    EndedAt            = NullableZdtToDateTime(sNode.Properties.TryGetValue("endedAt", out var ea) ? ea : null),
                    ObservationCount   = count,
                    RecentObservations = recentObs,
                };
            }).ToList();
        });

        return new MemoryContext
        {
            Project        = project,
            RecentSessions = sessionContexts,
        };
    }

    public async Task<int> CloseStaleSessionsAsync(
        TimeSpan staleThreshold,
        CancellationToken cancellationToken = default)
    {
        var cutoffIso = DateTimeOffset.UtcNow.Subtract(staleThreshold).ToString("o");
        await using var session = _context.Session();

        return await session.ExecuteWriteAsync(async tx =>
        {
            const string query = """
                MATCH (s:Session {isActive: true})
                WHERE s.startedAt < datetime($cutoff)
                SET s.isActive = false,
                    s.endedAt  = datetime(),
                    s.summary  = CASE WHEN s.summary IS NOT NULL THEN s.summary ELSE '[Auto-closed: stale session]' END
                RETURN count(s) AS closed
                """;

            var cursor = await tx.RunAsync(query,
                new Dictionary<string, object?> { ["cutoff"] = cutoffIso });
            var record = await cursor.SingleAsync();
            return (int)record["closed"].As<long>();
        });
    }

    public async Task<Session?> GetStaleActiveSessionAsync(
        string? project,
        TimeSpan staleThreshold,
        CancellationToken cancellationToken = default)
    {
        var cutoffIso = DateTimeOffset.UtcNow.Subtract(staleThreshold).ToString("o");
        await using var session = _context.Session();

        return await session.ExecuteReadAsync(async tx =>
        {
            const string query = """
                MATCH (s:Session {isActive: true})
                WHERE ($project IS NULL OR s.project = $project)
                  AND s.startedAt < datetime($cutoff)
                RETURN s
                ORDER BY s.startedAt DESC
                LIMIT 1
                """;

            var cursor = await tx.RunAsync(query, new Dictionary<string, object?>
            {
                ["project"] = (object?)project,
                ["cutoff"]  = cutoffIso,
            });

            if (!await cursor.FetchAsync())
                return null;

            return MapNode(cursor.Current["s"].As<INode>());
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Session MapNode(INode node)
    {
        var filesRaw = node.Properties.TryGetValue("filesChanged", out var fc) && fc is not null
            ? fc.As<List<object>>().Select(x => x.ToString() ?? string.Empty).ToArray()
            : Array.Empty<string>();

        return new Session
        {
            Id           = Guid.Parse(node["id"].As<string>()),
            Project      = node.Properties.TryGetValue("project", out var p)  ? p?.As<string>() : null,
            Goal         = node.Properties.TryGetValue("goal",    out var g)  ? g?.As<string>() : null,
            Summary      = node.Properties.TryGetValue("summary", out var su) ? su?.As<string>() : null,
            FilesChanged = filesRaw,
            StartedAt    = Neo4jMemoryRepository.ZdtToDateTime(node["startedAt"].As<ZonedDateTime>()),
            EndedAt      = NullableZdtToDateTime(node.Properties.TryGetValue("endedAt", out var ea) ? ea : null),
            IsActive     = node["isActive"].As<bool>(),
        };
    }

    private static DateTime? NullableZdtToDateTime(object? value) =>
        value is ZonedDateTime zdt ? Neo4jMemoryRepository.ZdtToDateTime(zdt) : null;
}
