using DevMemory.Cli.Commands;
using DevMemory.Cli.Infrastructure;
using DevMemory.Core.Interfaces;
using DevMemory.Infrastructure.Data;
using DevMemory.Infrastructure.Export;
using DevMemory.Infrastructure.Neo4j;
using DevMemory.Infrastructure.Neo4j.Extensions;
using DevMemory.Infrastructure.Search;
using DevMemory.Infrastructure.Sync;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;

// ── Configuration ─────────────────────────────────────────────────────────────

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables("DEVMEMORY_")
    .Build();

var provider = configuration["STORAGE"]
    ?? configuration["DevMemory:StorageProvider"]
    ?? "PostgreSQL";

// ── Services ──────────────────────────────────────────────────────────────────

var services = new ServiceCollection();
services.AddSingleton(configuration);

if (provider.Equals("SQLite", StringComparison.OrdinalIgnoreCase))
{
    var dbPath = configuration["SQLITE_PATH"]
        ?? configuration["DevMemory:SQLite:DatabasePath"]
        ?? "~/.devmemory/devmemory.db";

    var ctx = new SqliteContext(dbPath);
    ctx.EnsureSchemaAsync().GetAwaiter().GetResult();

    services.AddSingleton(ctx);
    services.AddSingleton<IMemoryRepository,  SqliteMemoryRepository>();
    services.AddSingleton<ISessionRepository, SqliteSessionRepository>();
    services.AddSingleton<ISearchService,     SqliteSearchService>();
}
else if (provider.Equals("Neo4j", StringComparison.OrdinalIgnoreCase))
{
    var uri      = configuration["NEO4J_URI"]      ?? configuration["DevMemory:Neo4j:Uri"]      ?? "bolt://localhost:7687";
    var user     = configuration["NEO4J_USER"]     ?? configuration["DevMemory:Neo4j:User"]     ?? "neo4j";
    var password = configuration["NEO4J_PASSWORD"] ?? configuration["DevMemory:Neo4j:Password"] ?? string.Empty;
    var database = configuration["NEO4J_DATABASE"] ?? configuration["DevMemory:Neo4j:Database"] ?? "neo4j";

    if (!string.IsNullOrWhiteSpace(password))
        services.AddNeo4jStorage(uri, user, password, database);
}
else
{
    var connectionString =
        configuration["POSTGRES_CONNECTION"]
        ?? configuration["DevMemory:PostgreSQL:ConnectionString"]
        ?? string.Empty;

    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        var ctx = new DapperContext(connectionString);
        services.AddSingleton(ctx);
        services.AddSingleton<IMemoryRepository,  PostgresMemoryRepository>();
        services.AddSingleton<ISessionRepository, PostgresSessionRepository>();
        services.AddSingleton<ISearchService,     PostgresSearchService>();
    }
}

services.AddSingleton<ExportService>();
services.AddSingleton(new SyncRuntimeOptions
{
    StorageProvider = provider,
    ConfiguredSyncPath = configuration["DevMemory:Sync:Path"],
});
services.AddSingleton<ISyncService, SyncService>();

// ── CLI App ───────────────────────────────────────────────────────────────────

var registrar = new TypeRegistrar(services);
var app = new CommandApp(registrar);

app.Configure(config =>
{
    config.SetApplicationName("devmemory");

    config.AddCommand<SaveCommand>("save")
        .WithDescription("Save a structured observation (decision, bugfix, pattern, etc.)")
        .WithExample("save", "\"Fixed N+1 query\"", "\"Added eager loading\"", "--type", "bugfix", "--project", "MyApp");

    config.AddCommand<SearchCommand>("search")
        .WithDescription("Full-text search across all memories")
        .WithExample("search", "\"multi-tenant\"", "--project", "SecurityV3", "--type", "security-pattern");

    config.AddCommand<ContextCommand>("context")
        .WithDescription("Show recent session context for a project")
        .WithExample("context", "SecurityV3");

    config.AddCommand<StatsCommand>("stats")
        .WithDescription("Show memory statistics");

    config.AddCommand<ExportCommand>("export")
        .WithDescription("Export observations to JSON or Markdown")
        .WithExample("export", "--project", "MyApp", "--format", "markdown", "--output", "memory.md");

    config.AddCommand<ImportCommand>("import")
        .WithDescription("Import observations from a JSON export file")
        .WithExample("import", "memory.json");

    config.AddCommand<SyncCommand>("sync")
        .WithDescription("Export/import file-based sync chunks for SQLite")
        .WithExample("sync", "--init", "--sync-path", "~/devmemory-sync")
        .WithExample("sync")
        .WithExample("sync", "--import")
        .WithExample("sync", "--status", "--sync-path", "~/devmemory-sync");

    config.AddCommand<McpCommand>("mcp")
        .WithDescription("Start the MCP server (stdio transport for AI agent integration)");

    config.AddCommand<ServeCommand>("serve")
        .WithDescription("Start the HTTP API server")
        .WithExample("serve", "--port", "7437");

    config.SetExceptionHandler((ex, _) =>
    {
        AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
        return -1;
    });
});

// Guard: warn if no storage is configured for commands that need it
var dblessCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "--help", "-h", "--version" };
if (args.Length > 0 && !dblessCommands.Contains(args[0]))
{
    var hasSqlite = provider.Equals("SQLite", StringComparison.OrdinalIgnoreCase);
    var hasPg     = !string.IsNullOrWhiteSpace(
        configuration["POSTGRES_CONNECTION"] ?? configuration["DevMemory:PostgreSQL:ConnectionString"]);
    var hasNeo4j  = provider.Equals("Neo4j", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(configuration["NEO4J_PASSWORD"] ?? configuration["DevMemory:Neo4j:Password"]);

    if (!hasSqlite && !hasPg && !hasNeo4j)
    {
        AnsiConsole.MarkupLine(
            "[red]Error:[/] No storage configured.\n" +
            "  PostgreSQL: set [bold]DEVMEMORY_POSTGRES_CONNECTION[/]\n" +
            "  SQLite:     set [bold]DEVMEMORY_STORAGE=SQLite[/]  (uses ~/.devmemory/devmemory.db)\n" +
            "  Neo4j:      set [bold]DEVMEMORY_STORAGE=Neo4j[/] and [bold]DEVMEMORY_NEO4J_PASSWORD[/]");
        return 1;
    }
}

return await app.RunAsync(args);
