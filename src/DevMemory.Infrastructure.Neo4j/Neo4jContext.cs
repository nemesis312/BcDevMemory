using Neo4j.Driver;

namespace DevMemory.Infrastructure.Neo4j;

/// <summary>
/// Manages the Neo4j driver lifecycle and provides sessions.
/// Initializes constraints and indexes on first use.
/// </summary>
public sealed class Neo4jContext : IAsyncDisposable
{
    private readonly IDriver _driver;
    private readonly string  _database;

    public Neo4jContext(string uri, string user, string password, string database = "neo4j")
    {
        _driver   = GraphDatabase.Driver(uri, AuthTokens.Basic(user, password));
        _database = database;
    }

    public IAsyncSession Session() =>
        _driver.AsyncSession(o => o.WithDatabase(_database));

    /// <summary>
    /// Idempotently creates constraints, indexes, and the full-text search index.
    /// Safe to call every startup.
    /// </summary>
    public async Task EnsureConstraintsAsync(CancellationToken cancellationToken = default)
    {
        await using var session = Session();

        // Unique constraints
        var constraints = new[]
        {
            "CREATE CONSTRAINT observation_id IF NOT EXISTS FOR (o:Observation) REQUIRE o.id IS UNIQUE",
            "CREATE CONSTRAINT session_id     IF NOT EXISTS FOR (s:Session)     REQUIRE s.id IS UNIQUE",
            "CREATE CONSTRAINT tag_name       IF NOT EXISTS FOR (t:Tag)         REQUIRE t.name IS UNIQUE",
            "CREATE CONSTRAINT project_name   IF NOT EXISTS FOR (p:Project)     REQUIRE p.name IS UNIQUE",
        };

        // Regular indexes
        var indexes = new[]
        {
            "CREATE INDEX observation_project IF NOT EXISTS FOR (o:Observation) ON (o.project)",
            "CREATE INDEX observation_type    IF NOT EXISTS FOR (o:Observation) ON (o.type)",
            "CREATE INDEX session_project     IF NOT EXISTS FOR (s:Session)     ON (s.project)",
            "CREATE INDEX session_active      IF NOT EXISTS FOR (s:Session)     ON (s.isActive)",
        };

        // Full-text index for search
        const string ftIndex =
            "CREATE FULLTEXT INDEX observation_search IF NOT EXISTS FOR (o:Observation) ON EACH [o.title, o.content]";

        // ExecuteWriteAsync does not accept a CancellationToken — execute each statement individually
        foreach (var stmt in constraints.Concat(indexes).Append(ftIndex))
        {
            var s = stmt; // capture for lambda
            await session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync(s);
                return 0;
            });
        }
    }

    public async ValueTask DisposeAsync() => await _driver.DisposeAsync();
}
