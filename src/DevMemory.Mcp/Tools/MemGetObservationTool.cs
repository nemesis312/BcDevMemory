using System.Text;
using System.Text.Json;
using DevMemory.Core.Interfaces;
using DevMemory.Mcp.Models;

namespace DevMemory.Mcp.Tools;

public sealed class MemGetObservationTool : IMcpTool
{
    private readonly IMemoryRepository _memory;

    public MemGetObservationTool(IMemoryRepository memory) => _memory = memory;

    public string Name        => "mem_get_observation";
    public string Description => "Get the full content of a specific observation by ID.";
    public object InputSchema => new
    {
        type       = "object",
        properties = new
        {
            id = new { type = "string", description = "Observation UUID (from mem_search results)" },
        },
        required = new[] { "id" },
    };

    public async Task<McpResponse> ExecuteAsync(
        object? requestId, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (!arguments.TryGetProperty("id", out var idEl) || idEl.GetString() is not { } idStr)
            return McpResponse.InvalidParams(requestId, "'id' is required");

        if (!Guid.TryParse(idStr, out var id))
            return McpResponse.InvalidParams(requestId, $"'{idStr}' is not a valid UUID");

        var obs = await _memory.GetObservationAsync(id, cancellationToken);
        if (obs is null)
            return McpResponse.ToolError(requestId, $"Observation {id:N} not found.");

        var sb = new StringBuilder();
        sb.AppendLine($"[{obs.Type}] {obs.Title}");
        sb.AppendLine($"Project:  {obs.Project ?? "(none)"}");
        sb.AppendLine($"Saved:    {obs.CreatedAt:yyyy-MM-dd HH:mm} UTC");
        if (obs.Tags.Length > 0)
            sb.AppendLine($"Tags:     {string.Join(", ", obs.Tags)}");
        sb.AppendLine();
        sb.AppendLine(obs.Content);

        return McpResponse.ToolSuccess(requestId, sb.ToString().TrimEnd());
    }
}
