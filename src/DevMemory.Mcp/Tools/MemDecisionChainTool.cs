using System.Text;
using System.Text.Json;
using DevMemory.Core.Interfaces;
using DevMemory.Mcp.Models;

namespace DevMemory.Mcp.Tools;

/// <summary>
/// mem_decision_chain — trace the chain of observations that led to a decision.
/// Follows RELATES_TO edges backward from a decision-type observation.
/// Only available when using Neo4j storage.
/// </summary>
public sealed class MemDecisionChainTool : IMcpTool
{
    private readonly IGraphRepository _graph;

    public MemDecisionChainTool(IGraphRepository graph)
    {
        _graph = graph;
    }

    public string Name        => "mem_decision_chain";
    public string Description => "Trace the history behind a decision — find all observations that contributed to it. Neo4j only.";
    public object InputSchema => new
    {
        type       = "object",
        properties = new
        {
            id = new { type = "string", format = "uuid", description = "ID of the decision-type observation" },
        },
        required = new[] { "id" },
    };

    public async Task<McpResponse> ExecuteAsync(
        object? requestId, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (!arguments.TryGetProperty("id", out var idEl) || idEl.GetString() is not { Length: > 0 } idStr)
            return McpResponse.InvalidParams(requestId, "'id' is required");

        if (!Guid.TryParse(idStr, out var decisionId))
            return McpResponse.InvalidParams(requestId, "'id' must be a valid UUID");

        var chain = (await _graph.GetDecisionChainAsync(decisionId, cancellationToken)).ToList();

        if (chain.Count == 0)
            return McpResponse.ToolSuccess(requestId,
                "No predecessor observations found. Either this is not a 'decision' type, " +
                "or no RELATES_TO relationships have been created pointing to it.");

        var sb = new StringBuilder();
        sb.AppendLine($"Decision chain — {chain.Count} predecessor(s):");
        sb.AppendLine();

        foreach (var obs in chain)
        {
            sb.AppendLine($"• [{obs.Type}] {obs.Title}");
            sb.AppendLine($"  ID: {obs.Id:N}  |  {obs.CreatedAt:yyyy-MM-dd}");
            if (obs.Project is not null)
                sb.AppendLine($"  Project: {obs.Project}");
            sb.AppendLine();
        }

        return McpResponse.ToolSuccess(requestId, sb.ToString().TrimEnd());
    }
}
