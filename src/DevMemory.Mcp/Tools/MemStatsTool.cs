using System.Text;
using System.Text.Json;
using DevMemory.Core.Interfaces;
using DevMemory.Mcp.Models;

namespace DevMemory.Mcp.Tools;

public sealed class MemStatsTool : IMcpTool
{
    private readonly IMemoryRepository _memory;

    public MemStatsTool(IMemoryRepository memory) => _memory = memory;

    public string Name        => "mem_stats";
    public string Description => "Show memory usage statistics — total observations, sessions, and projects tracked.";
    public object InputSchema => new
    {
        type       = "object",
        properties = new { },
    };

    public async Task<McpResponse> ExecuteAsync(
        object? requestId, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        var stats = await _memory.GetStatsAsync(cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("DevMemory Statistics");
        sb.AppendLine("────────────────────");
        sb.AppendLine($"Observations : {stats.TotalObservations}");
        sb.AppendLine($"Sessions     : {stats.TotalSessions} ({stats.ActiveSessions} active)");
        sb.AppendLine($"Projects     : {stats.Projects}");

        if (stats.OldestObservation.HasValue)
            sb.AppendLine($"Oldest       : {stats.OldestObservation.Value:yyyy-MM-dd}");
        if (stats.NewestObservation.HasValue)
            sb.AppendLine($"Newest       : {stats.NewestObservation.Value:yyyy-MM-dd HH:mm} UTC");

        return McpResponse.ToolSuccess(requestId, sb.ToString().TrimEnd());
    }
}
