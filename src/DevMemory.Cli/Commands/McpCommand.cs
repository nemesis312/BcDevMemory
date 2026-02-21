using DevMemory.Core.Interfaces;
using DevMemory.Mcp.Server;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace DevMemory.Cli.Commands;

/// <summary>
/// Starts the MCP server over stdio so AI agents (Claude Code, Cursor, etc.)
/// can connect via the Model Context Protocol JSON-RPC 2.0 transport.
/// Graph tools (mem_link, mem_related, mem_decision_chain) are automatically
/// enabled when Neo4j storage is configured.
/// </summary>
internal sealed class McpCommand : AsyncCommand
{
    private readonly IMemoryRepository  _memory;
    private readonly ISessionRepository _sessions;
    private readonly ISearchService     _search;
    private readonly IGraphRepository?  _graph;

    public McpCommand(
        IMemoryRepository  memory,
        ISessionRepository sessions,
        ISearchService     search,
        IServiceProvider   services)
    {
        _memory   = memory;
        _sessions = sessions;
        _search   = search;
        // Null when using SQLite/PostgreSQL — McpServer omits graph tools gracefully.
        _graph    = services.GetService<IGraphRepository>();
    }

    public override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var server = new McpServer(_memory, _sessions, _search, _graph);
        await server.RunAsync(cancellationToken);
        return 0;
    }
}
