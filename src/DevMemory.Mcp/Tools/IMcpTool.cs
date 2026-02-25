using System.Text.Json;
using DevMemory.Mcp.Models;

namespace DevMemory.Mcp.Tools;

public interface IMcpTool
{
    string Name { get; }
    string Description { get; }
    object InputSchema { get; }

    /// <summary>
    /// When true, the tool only reads data and never mutates state.
    /// MCP clients use this hint (annotations.readOnlyHint) to allow the tool
    /// in restricted contexts such as Claude Code plan mode.
    /// </summary>
    bool IsReadOnly => false;

    Task<McpResponse> ExecuteAsync(
        object? requestId,
        JsonElement arguments,
        CancellationToken cancellationToken = default);
}
