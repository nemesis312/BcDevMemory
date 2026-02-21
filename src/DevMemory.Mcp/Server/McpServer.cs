using DevMemory.Core.Interfaces;
using DevMemory.Mcp.Tools;

namespace DevMemory.Mcp.Server;

/// <summary>
/// Composes all MCP tools, wires them to the JSON-RPC handler,
/// and runs the stdio transport loop.
///
/// Usage (CLI McpCommand):
///   var server = new McpServer(memory, sessions, search);
///   await server.RunAsync(cancellationToken);
///
/// With Neo4j graph tools:
///   var server = new McpServer(memory, sessions, search, graphRepository);
///   await server.RunAsync(cancellationToken);
/// </summary>
public sealed class McpServer
{
    private readonly JsonRpcHandler  _handler;
    private readonly StdioTransport  _transport;

    public McpServer(
        IMemoryRepository  memory,
        ISessionRepository sessions,
        ISearchService     search,
        IGraphRepository?  graph = null)
    {
        var tools = new List<IMcpTool>
        {
            new MemSaveTool           (memory, sessions),
            new MemSearchTool         (search),
            new MemContextTool        (sessions),
            new MemSessionSummaryTool (sessions),
            new MemTimelineTool       (search),
            new MemGetObservationTool (memory),
            new MemStatsTool          (memory),
            new MemSessionStartTool   (sessions),
            new MemSessionEndTool     (sessions),
            new MemSavePromptTool     (memory, sessions),
        };

        // Graph tools are only registered when a graph backend (Neo4j) is configured
        if (graph is not null)
        {
            tools.Add(new MemLinkTool         (graph));
            tools.Add(new MemRelatedTool      (graph));
            tools.Add(new MemDecisionChainTool(graph));
        }

        _handler   = new JsonRpcHandler(tools);
        _transport = new StdioTransport();
    }

    public Task RunAsync(CancellationToken cancellationToken = default) =>
        _transport.RunAsync(_handler, cancellationToken);
}
