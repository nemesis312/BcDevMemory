using System.Text.Json;
using DevMemory.Core.Interfaces;
using DevMemory.Mcp.Models;

namespace DevMemory.Mcp.Tools;

/// <summary>
/// mem_link — create a RELATES_TO relationship between two observations.
/// Only available when using Neo4j storage.
/// </summary>
public sealed class MemLinkTool : IMcpTool
{
    private readonly IGraphRepository _graph;

    public MemLinkTool(IGraphRepository graph)
    {
        _graph = graph;
    }

    public string Name        => "mem_link";
    public string Description => "Link two observations with a typed relationship (reason + strength). Neo4j only.";
    public object InputSchema => new
    {
        type       = "object",
        properties = new
        {
            fromId   = new { type = "string", format = "uuid", description = "Source observation ID" },
            toId     = new { type = "string", format = "uuid", description = "Target observation ID" },
            reason   = new { type = "string", description = "Why these observations are related (optional)" },
            strength = new { type = "number", minimum = 0, maximum = 1, description = "Relationship strength 0.0–1.0 (default: 1.0)" },
        },
        required = new[] { "fromId", "toId" },
    };

    public async Task<McpResponse> ExecuteAsync(
        object? requestId, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (!arguments.TryGetProperty("fromId", out var fromEl) || fromEl.GetString() is not { Length: > 0 } fromStr)
            return McpResponse.InvalidParams(requestId, "'fromId' is required");
        if (!arguments.TryGetProperty("toId",   out var toEl)   || toEl.GetString()   is not { Length: > 0 } toStr)
            return McpResponse.InvalidParams(requestId, "'toId' is required");

        if (!Guid.TryParse(fromStr, out var fromId))
            return McpResponse.InvalidParams(requestId, "'fromId' must be a valid UUID");
        if (!Guid.TryParse(toStr, out var toId))
            return McpResponse.InvalidParams(requestId, "'toId' must be a valid UUID");

        var reason   = arguments.TryGetProperty("reason",   out var rEl) ? rEl.GetString() : null;
        var strength = arguments.TryGetProperty("strength", out var sEl) && sEl.ValueKind == JsonValueKind.Number
            ? (float)sEl.GetDouble()
            : 1.0f;

        await _graph.LinkObservationsAsync(fromId, toId, reason, strength, cancellationToken);

        return McpResponse.ToolSuccess(requestId,
            $"✓ Linked {fromStr[..8]}... → {toStr[..8]}..." +
            (reason is not null ? $" ({reason})" : string.Empty));
    }
}
