using System.Text;
using System.Text.Json;
using DevMemory.Core.Interfaces;
using DevMemory.Mcp.Models;

namespace DevMemory.Mcp.Tools;

/// <summary>
/// mem_related — traverse RELATES_TO relationships to find connected observations.
/// Only available when using Neo4j storage.
/// </summary>
public sealed class MemRelatedTool : IMcpTool
{
    private readonly IGraphRepository _graph;

    public MemRelatedTool(IGraphRepository graph)
    {
        _graph = graph;
    }

    public string Name        => "mem_related";
    public bool   IsReadOnly  => true;
    public string Description => "Find observations transitively related to a given observation (graph traversal). Neo4j only.";
    public object InputSchema => new
    {
        type       = "object",
        properties = new
        {
            id    = new { type = "string", format = "uuid", description = "Starting observation ID" },
            depth = new { type = "integer", minimum = 1, maximum = 5, description = "Traversal depth (default: 2)" },
        },
        required = new[] { "id" },
    };

    public async Task<McpResponse> ExecuteAsync(
        object? requestId, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (!arguments.TryGetProperty("id", out var idEl) || idEl.GetString() is not { Length: > 0 } idStr)
            return McpResponse.InvalidParams(requestId, "'id' is required");

        if (!Guid.TryParse(idStr, out var observationId))
            return McpResponse.InvalidParams(requestId, "'id' must be a valid UUID");

        var depth = arguments.TryGetProperty("depth", out var dEl) && dEl.ValueKind == JsonValueKind.Number
            ? dEl.GetInt32()
            : 2;

        var related = (await _graph.GetRelatedAsync(observationId, depth, cancellationToken)).ToList();

        if (related.Count == 0)
            return McpResponse.ToolSuccess(requestId, "No related observations found.");

        var sb = new StringBuilder();
        sb.AppendLine($"Found {related.Count} related observation(s):");
        sb.AppendLine();

        foreach (var obs in related)
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
