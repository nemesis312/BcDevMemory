namespace DevMemory.Mcp.Server;

/// <summary>
/// MCP protocol constants shared across transports and handlers.
/// </summary>
public static class McpConstants
{
    /// <summary>
    /// The MCP spec revision this server implements.
    ///
    /// MCP uses date-based versioning. Each date identifies a spec revision
    /// with a specific feature set. The server returns this value in the
    /// <c>initialize</c> response so clients know which capabilities are available.
    ///
    /// Known revisions:
    ///   2024-11-05 — first stable release; defines stdio + SSE transports.
    ///   2025-03-26 — adds streamable HTTP, OAuth, notifications/list_changed.
    ///
    /// Update this value only when the server actually implements the new spec features.
    /// Spec reference: https://spec.modelcontextprotocol.io/specification/
    /// </summary>
    public const string ProtocolVersion = "2024-11-05";

    public const string ServerName = "devmemory";
    public const string ServerVersion = "1.0.1";
}
