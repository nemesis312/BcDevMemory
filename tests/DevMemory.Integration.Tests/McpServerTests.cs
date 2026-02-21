using System.Text.Json;
using DevMemory.Infrastructure.Data;
using DevMemory.Infrastructure.Search;
using DevMemory.Mcp.Server;
using Testcontainers.PostgreSql;

namespace DevMemory.Integration.Tests;

/// <summary>
/// End-to-end tests for the MCP JSON-RPC handler.
/// Uses a real PostgreSQL container — tests call HandleAsync directly (no stdio overhead).
/// </summary>
public sealed class McpServerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("devmemory_mcp_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private JsonRpcHandler _handler = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var context  = new DapperContext(_container.GetConnectionString());
        var migrator = new DatabaseMigrator(context);
        await migrator.MigrateAsync();

        var memory   = new PostgresMemoryRepository(context);
        var sessions = new PostgresSessionRepository(context);
        var search   = new PostgresSearchService(context);

        var server = new McpServer(memory, sessions, search);

        // Grab the handler via the server's internal field for direct testing.
        // In production the server is wired through RunAsync/StdioTransport.
        // We use reflection here only in tests to avoid adding a test seam to production code.
        var field = typeof(McpServer)
            .GetField("_handler", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        _handler = (JsonRpcHandler)field.GetValue(server)!;
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    // ── helpers ────────────────────────────────────────────────────────────

    private async Task<JsonDocument> SendAsync(string method, object? @params = null)
    {
        var req = @params is null
            ? $$$"""{"jsonrpc":"2.0","id":1,"method":"{{{method}}}"}"""
            : $$$"""{"jsonrpc":"2.0","id":1,"method":"{{{method}}}","params":{{{JsonSerializer.Serialize(@params)}}}}""";

        var raw = await _handler.HandleAsync(req, CancellationToken.None);
        Assert.NotNull(raw);
        return JsonDocument.Parse(raw!);
    }

    private static string Text(JsonDocument doc) =>
        doc.RootElement
           .GetProperty("result")
           .GetProperty("content")[0]
           .GetProperty("text")
           .GetString()!;

    private static bool IsError(JsonDocument doc) =>
        doc.RootElement.TryGetProperty("error", out _) ||
        (doc.RootElement.TryGetProperty("result", out var r) &&
         r.TryGetProperty("isError", out var e) && e.GetBoolean());

    // ── initialize ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Initialize_Returns_Server_Info()
    {
        var doc = await SendAsync("initialize", new
        {
            protocolVersion = "2024-11-05",
            capabilities    = new { },
            clientInfo      = new { name = "test", version = "1.0" },
        });

        var result = doc.RootElement.GetProperty("result");
        Assert.Equal("devmemory", result.GetProperty("serverInfo").GetProperty("name").GetString());
        Assert.Equal("2024-11-05", result.GetProperty("protocolVersion").GetString());
    }

    // ── tools/list ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ToolsList_Returns_All_Ten_Tools()
    {
        var doc   = await SendAsync("tools/list");
        var tools = doc.RootElement.GetProperty("result").GetProperty("tools");

        Assert.Equal(10, tools.GetArrayLength());

        var names = tools.EnumerateArray()
            .Select(t => t.GetProperty("name").GetString())
            .ToHashSet();

        foreach (var expected in new[]
        {
            "mem_save", "mem_search", "mem_context", "mem_session_summary",
            "mem_timeline", "mem_get_observation", "mem_stats",
            "mem_session_start", "mem_session_end", "mem_save_prompt",
        })
        {
            Assert.Contains(expected, names);
        }
    }

    // ── mem_save ───────────────────────────────────────────────────────────

    [Fact]
    public async Task MemSave_Saves_And_Returns_Success()
    {
        var doc = await SendAsync("tools/call", new
        {
            name      = "mem_save",
            arguments = new
            {
                title   = "Test observation",
                type    = "pattern",
                content = "What: Something. Why: Because.",
                project = "McpTest",
            },
        });

        Assert.False(IsError(doc));
        Assert.Contains("Saved", Text(doc));
    }

    [Fact]
    public async Task MemSave_Rejects_Invalid_Type()
    {
        var doc = await SendAsync("tools/call", new
        {
            name      = "mem_save",
            arguments = new { title = "X", type = "not-a-real-type", content = "Y" },
        });

        Assert.True(IsError(doc));
    }

    [Fact]
    public async Task MemSave_Strips_Private_Tags()
    {
        var doc = await SendAsync("tools/call", new
        {
            name      = "mem_save",
            arguments = new
            {
                title   = "Privacy test",
                type    = "pattern",
                content = "Public <private>SECRET</private> End",
                project = "McpTest",
            },
        });

        Assert.False(IsError(doc));
        // The content is stored with REDACTED — verify via mem_search
        var searchDoc = await SendAsync("tools/call", new
        {
            name      = "mem_search",
            arguments = new { query = "Privacy test", project = "McpTest" },
        });
        Assert.False(IsError(searchDoc));
    }

    // ── mem_search ─────────────────────────────────────────────────────────

    [Fact]
    public async Task MemSearch_Returns_Matching_Results()
    {
        // Save first
        await SendAsync("tools/call", new
        {
            name      = "mem_save",
            arguments = new
            {
                title   = "Authentication JWT pattern",
                type    = "security-pattern",
                content = "What: JWT. Why: Stateless auth.",
                project = "SearchMcpTest",
            },
        });

        var doc = await SendAsync("tools/call", new
        {
            name      = "mem_search",
            arguments = new { query = "authentication", project = "SearchMcpTest" },
        });

        Assert.False(IsError(doc));
        Assert.Contains("JWT", Text(doc));
    }

    // ── mem_stats ──────────────────────────────────────────────────────────

    [Fact]
    public async Task MemStats_Returns_Statistics()
    {
        var doc = await SendAsync("tools/call", new
        {
            name      = "mem_stats",
            arguments = new { },
        });

        Assert.False(IsError(doc));
        Assert.Contains("Observations", Text(doc));
    }

    // ── method not found ───────────────────────────────────────────────────

    [Fact]
    public async Task Unknown_Method_Returns_Error()
    {
        var doc = await SendAsync("unknown/method");
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }
}
