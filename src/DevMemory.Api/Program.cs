using DevMemory.Api.Endpoints;
using DevMemory.Core.Interfaces;
using DevMemory.Infrastructure.Data;
using DevMemory.Infrastructure.Export;
using DevMemory.Infrastructure.Neo4j;
using DevMemory.Infrastructure.Neo4j.Extensions;
using DevMemory.Infrastructure.Search;
using DevMemory.Infrastructure.Sync;

// ── Configuration ─────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

var provider = Environment.GetEnvironmentVariable("DEVMEMORY_STORAGE")
    ?? builder.Configuration["DevMemory:StorageProvider"]
    ?? "PostgreSQL";

// ── Services ──────────────────────────────────────────────────────────────────

if (provider.Equals("SQLite", StringComparison.OrdinalIgnoreCase))
{
    var dbPath = Environment.GetEnvironmentVariable("DEVMEMORY_SQLITE_PATH")
        ?? builder.Configuration["DevMemory:SQLite:DatabasePath"]
        ?? "~/.devmemory/devmemory.db";

    var ctx = new SqliteContext(dbPath);
    ctx.EnsureSchemaAsync().GetAwaiter().GetResult();

    builder.Services.AddSingleton(ctx);
    builder.Services.AddSingleton<IMemoryRepository,  SqliteMemoryRepository>();
    builder.Services.AddSingleton<ISessionRepository, SqliteSessionRepository>();
    builder.Services.AddSingleton<ISearchService,     SqliteSearchService>();
}
else if (provider.Equals("Neo4j", StringComparison.OrdinalIgnoreCase))
{
    var uri      = Environment.GetEnvironmentVariable("DEVMEMORY_NEO4J_URI")
        ?? builder.Configuration["DevMemory:Neo4j:Uri"]
        ?? "bolt://localhost:7687";
    var user     = Environment.GetEnvironmentVariable("DEVMEMORY_NEO4J_USER")
        ?? builder.Configuration["DevMemory:Neo4j:User"]
        ?? "neo4j";
    var password = Environment.GetEnvironmentVariable("DEVMEMORY_NEO4J_PASSWORD")
        ?? builder.Configuration["DevMemory:Neo4j:Password"]
        ?? throw new InvalidOperationException(
            "No Neo4j password configured. Set DEVMEMORY_NEO4J_PASSWORD or DevMemory:Neo4j:Password.");
    var database = Environment.GetEnvironmentVariable("DEVMEMORY_NEO4J_DATABASE")
        ?? builder.Configuration["DevMemory:Neo4j:Database"]
        ?? "neo4j";

    builder.Services.AddNeo4jStorage(uri, user, password, database);
}
else
{
    var connectionString =
        Environment.GetEnvironmentVariable("DEVMEMORY_POSTGRES_CONNECTION")
        ?? builder.Configuration["DevMemory:PostgreSQL:ConnectionString"]
        ?? throw new InvalidOperationException(
            "No PostgreSQL connection string configured. " +
            "Set DEVMEMORY_POSTGRES_CONNECTION or DevMemory:PostgreSQL:ConnectionString.");

    builder.Services.AddSingleton(new DapperContext(connectionString));
    builder.Services.AddSingleton<IMemoryRepository,  PostgresMemoryRepository>();
    builder.Services.AddSingleton<ISessionRepository, PostgresSessionRepository>();
    builder.Services.AddSingleton<ISearchService,     PostgresSearchService>();
}

builder.Services.AddSingleton<ExportService>();
builder.Services.AddSingleton(new SyncRuntimeOptions
{
    StorageProvider = provider,
    ConfiguredSyncPath = Environment.GetEnvironmentVariable("DEVMEMORY_SYNC_PATH")
        ?? builder.Configuration["DevMemory:Sync:Path"],
});
builder.Services.AddSingleton<ISyncService, SyncService>();
builder.Services.AddEndpointsApiExplorer();

var port = builder.Configuration.GetValue<int>("DevMemory:Server:Port", 7437);
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// ── App ───────────────────────────────────────────────────────────────────────

var app = builder.Build();

app.MapMemoryEndpoints();
app.MapSearchEndpoints();
app.MapSessionEndpoints();
app.MapExportEndpoints();
app.MapSyncEndpoints();

app.MapGet("/", () => Results.Ok(new
{
    service  = "DevMemory API",
    version  = "1.0",
    provider = provider,
}));

app.Run();
