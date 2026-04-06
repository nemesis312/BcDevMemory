using DevMemory.Core.Interfaces;
using DevMemory.Mcp.Tools;

namespace DevMemory.Mcp.Server;

/// <summary>
/// Composes all MCP tools, wires them to the JSON-RPC handler,
/// and delegates execution to the selected transport.
///
/// Stdio (default — local mode):
///   var server = new McpServer(memory, sessions, search);
///
/// SSE (HTTP mode — containerized/centralized):
///   var server = new McpServer(memory, sessions, search, transport: new SseTransport(8080));
///
/// With Neo4j graph tools, pass the graph repository as the fourth argument.
/// With bc-agentic-os artifact + run-state tools, pass the optional repositories.
/// </summary>
public sealed class McpServer
{
    private readonly JsonRpcHandler _handler;
    private readonly ITransport     _transport;

    public McpServer(
        IMemoryRepository     memory,
        ISessionRepository    sessions,
        ISearchService        search,
        IGraphRepository?     graph     = null,
        ITransport?           transport = null,
        IArtifactRepository?  artifacts = null,
        IRunStateRepository?  runStates = null)
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

        // bc-agentic-os artifact tools
        if (artifacts is not null)
        {
            tools.Add(new ArtifactSaveTool        (artifacts));
            tools.Add(new ArtifactGetTool         (artifacts));
            tools.Add(new ArtifactListRunTool     (artifacts));
            tools.Add(new ArtifactListProjectTool (artifacts));
        }

        // bc-agentic-os run state tools
        if (runStates is not null)
        {
            tools.Add(new RunCheckpointSaveTool (runStates));
            tools.Add(new RunListActiveTool     (runStates));
        }

        // run_context_get requires both repositories
        if (artifacts is not null && runStates is not null)
        {
            tools.Add(new RunContextGetTool(runStates, artifacts));
        }

        _handler   = new JsonRpcHandler(tools);
        _transport = transport ?? new StdioTransport();
    }

    public Task RunAsync(CancellationToken cancellationToken = default) =>
        _transport.RunAsync(_handler, cancellationToken);
}
