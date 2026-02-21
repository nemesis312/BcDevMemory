using System.Text.Json;
using DevMemory.Mcp.Models;

namespace DevMemory.Mcp.Tools;

public interface IMcpTool
{
    string Name { get; }
    string Description { get; }
    object InputSchema { get; }

    Task<McpResponse> ExecuteAsync(
        object? requestId,
        JsonElement arguments,
        CancellationToken cancellationToken = default);
}
