using System.Text;
using System.Text.Json;
using DevMemory.Core.Interfaces;
using DevMemory.Mcp.Models;

namespace DevMemory.Mcp.Tools;

public sealed class MemTimelineTool : IMcpTool
{
    private readonly ISearchService _search;

    public MemTimelineTool(ISearchService search) => _search = search;

    public string Name        => "mem_timeline";
    public bool   IsReadOnly  => true;
    public string Description => "Show observations chronologically around a specific observation — useful for understanding context.";
    public object InputSchema => new
    {
        type       = "object",
        properties = new
        {
            observation_id = new { type = "string",  description = "Anchor observation UUID" },
            before         = new { type = "integer", description = "Observations to show before (default 3)" },
            after          = new { type = "integer", description = "Observations to show after (default 3)" },
        },
        required = new[] { "observation_id" },
    };

    public async Task<McpResponse> ExecuteAsync(
        object? requestId, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (!arguments.TryGetProperty("observation_id", out var idEl) || idEl.GetString() is not { } idStr)
            return McpResponse.InvalidParams(requestId, "'observation_id' is required");

        if (!Guid.TryParse(idStr, out var id))
            return McpResponse.InvalidParams(requestId, $"'{idStr}' is not a valid UUID");

        var before = arguments.TryGetProperty("before", out var bEl) && bEl.TryGetInt32(out var b) ? b : 3;
        var after  = arguments.TryGetProperty("after",  out var aEl) && aEl.TryGetInt32(out var a) ? a : 3;

        var timeline = (await _search.GetTimelineAsync(id, before, after, cancellationToken)).ToList();

        if (timeline.Count == 0)
            return McpResponse.ToolError(requestId, $"Observation {id:N} not found.");

        var sb = new StringBuilder();
        sb.AppendLine("Timeline:");
        sb.AppendLine();

        foreach (var obs in timeline)
        {
            var marker = obs.Id == id ? "▶" : " ";
            sb.AppendLine($"{marker} {obs.CreatedAt:yyyy-MM-dd HH:mm}  [{obs.Type}] {obs.Title}");
            sb.AppendLine($"  ID: {obs.Id:N}");
        }

        return McpResponse.ToolSuccess(requestId, sb.ToString().TrimEnd());
    }
}
