using DevMemory.Core.Interfaces;
using DevMemory.Infrastructure.Data;
using DevMemory.Infrastructure.Neo4j;
using DevMemory.Infrastructure.Search;
using DevMemory.Mcp.Server;

var storage = Environment.GetEnvironmentVariable("DEVMEMORY_STORAGE") ?? "SQLite";

IMemoryRepository memory;
ISessionRepository sessions;
ISearchService search;
IGraphRepository? graph = null;
IAsyncDisposable? asyncDisposable = null;
IDisposable? contextDisposable = null;

if (storage.Equals("SQLite", StringComparison.OrdinalIgnoreCase))
{
    var dbPath = Environment.GetEnvironmentVariable("DEVMEMORY_SQLITE_PATH")
        ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".devmemory", "devmemory.db");

    var ctx = new SqliteContext(dbPath);
    await ctx.EnsureSchemaAsync();

    memory = new SqliteMemoryRepository(ctx);
    sessions = new SqliteSessionRepository(ctx);
    search = new SqliteSearchService(ctx);

    await Console.Error.WriteLineAsync($"[devmemory] Using SQLite: {dbPath}");
}
else if (storage.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
{
    var connectionString =
        Environment.GetEnvironmentVariable("DEVMEMORY_POSTGRES_CONNECTION")
        ?? throw new InvalidOperationException(
            "PostgreSQL connection string not configured. Set DEVMEMORY_POSTGRES_CONNECTION environment variable.");

    var ctx = new DapperContext(connectionString);
    contextDisposable = ctx;

    memory = new PostgresMemoryRepository(ctx);
    sessions = new PostgresSessionRepository(ctx);
    search = new PostgresSearchService(ctx);

    await Console.Error.WriteLineAsync($"[devmemory] Using PostgreSQL");
}
else if (storage.Equals("Neo4j", StringComparison.OrdinalIgnoreCase))
{
    var uri      = Environment.GetEnvironmentVariable("DEVMEMORY_NEO4J_URI")      ?? "bolt://localhost:7687";
    var user     = Environment.GetEnvironmentVariable("DEVMEMORY_NEO4J_USER")     ?? "neo4j";
    var password = Environment.GetEnvironmentVariable("DEVMEMORY_NEO4J_PASSWORD")
        ?? throw new InvalidOperationException(
            "Neo4j password not configured. Set DEVMEMORY_NEO4J_PASSWORD environment variable.");
    var database = Environment.GetEnvironmentVariable("DEVMEMORY_NEO4J_DATABASE") ?? "neo4j";

    var ctx = new Neo4jContext(uri, user, password, database);
    asyncDisposable = ctx;
    await ctx.EnsureConstraintsAsync();

    var repo = new Neo4jMemoryRepository(ctx);
    memory   = repo;
    graph    = repo;
    sessions = new Neo4jSessionRepository(ctx);
    search   = new Neo4jSearchService(ctx);

    await Console.Error.WriteLineAsync($"[devmemory] Using Neo4j: {uri}/{database}");
}
else
{
    await Console.Error.WriteLineAsync($"[devmemory] Unknown storage provider: {storage}. Use 'SQLite', 'PostgreSQL', or 'Neo4j'.");
    return 1;
}

var transportMode = Environment.GetEnvironmentVariable("DEVMEMORY_TRANSPORT") ?? "stdio";
var port          = int.TryParse(Environment.GetEnvironmentVariable("DEVMEMORY_PORT"), out var p) ? p : 8080;

ITransport transport = transportMode.Equals("http", StringComparison.OrdinalIgnoreCase)
    ? new SseTransport(port)
    : new StdioTransport();

var server = new McpServer(memory, sessions, search, graph, transport);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

try
{
    await server.RunAsync(cts.Token);
}
finally
{
    contextDisposable?.Dispose();
    if (asyncDisposable is not null)
        await asyncDisposable.DisposeAsync();
}

return 0;
