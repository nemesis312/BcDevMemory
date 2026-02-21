using DevMemory.Core.Entities;
using DevMemory.Core.Interfaces;
using Neo4j.Driver;

namespace DevMemory.Infrastructure.Neo4j;

/// <summary>
/// Implements ISearchService using Neo4j full-text index and graph traversal.
/// The full-text index "observation_search" covers Observation.title and Observation.content.
/// </summary>
public sealed class Neo4jSearchService : ISearchService
{
    private readonly Neo4jContext _context;

    public Neo4jSearchService(Neo4jContext context)
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
        await using var session = _context.Session();

        return await session.ExecuteReadAsync(async tx =>
        {
            const string cypher = """
                CALL db.index.fulltext.queryNodes('observation_search', $query)
                YIELD node, score
                WHERE ($project IS NULL OR node.project = $project)
                  AND ($type    IS NULL OR node.type    = $type)
                OPTIONAL MATCH (node)-[:TAGGED_WITH]->(t:Tag)
                WITH node, score, collect(t.name) AS nodeTags
                WHERE $tags IS NULL OR all(tag IN $tags WHERE tag IN nodeTags)
                RETURN node, score
                ORDER BY score DESC
                LIMIT $limit
                """;

            var cursor = await tx.RunAsync(cypher, new Dictionary<string, object?>
            {
                ["query"]   = query,
                ["project"] = (object?)project,
                ["type"]    = (object?)type,
                ["tags"]    = tags != null ? (object)tags : null,
                ["limit"]   = limit,
            });

            var records = await cursor.ToListAsync();
            return records.Select(r =>
            {
                var node  = r["node"].As<INode>();
                var score = (float)r["score"].As<double>();
                var obs   = MapNode(node);
                obs.Rank           = score;
                obs.ContentPreview = obs.Content.Length > 200 ? obs.Content[..200] : obs.Content;
                obs.Content        = string.Empty; // caller uses GetObservation for full content
                return obs;
            }).ToList();
        });
    }

    public async Task<IEnumerable<Observation>> GetTimelineAsync(
        Guid observationId,
        int beforeCount = 3,
        int afterCount = 3,
        CancellationToken cancellationToken = default)
    {
        await using var session = _context.Session();

        return await session.ExecuteReadAsync(async tx =>
        {
            // Fetch all observations in the same session, then slice in C# around the target.
            const string query = """
                MATCH (target:Observation {id: $id})
                MATCH (s:Session)-[:CONTAINS]->(target)
                MATCH (s)-[:CONTAINS]->(o:Observation)
                RETURN o
                ORDER BY o.createdAt ASC
                """;

            var cursor = await tx.RunAsync(query,
                new Dictionary<string, object?> { ["id"] = observationId.ToString() });
            var records = await cursor.ToListAsync();
            var allObs  = records.Select(r => MapNode(r["o"].As<INode>())).ToList();

            var targetIndex = allObs.FindIndex(o => o.Id == observationId);
            if (targetIndex < 0)
                return allObs;

            var start = Math.Max(0, targetIndex - beforeCount);
            var end   = Math.Min(allObs.Count, targetIndex + afterCount + 1);
            return allObs.GetRange(start, end - start);
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
            CreatedAt = Neo4jMemoryRepository.ZdtToDateTime(node["createdAt"].As<ZonedDateTime>()),
        };
    }
}
