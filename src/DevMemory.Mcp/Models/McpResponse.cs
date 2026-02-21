using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevMemory.Mcp.Models;

/// <summary>
/// JSON-RPC 2.0 response envelope.
/// Exactly one of Result or Error must be set.
/// </summary>
public sealed class McpResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public object? Id { get; init; }

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; init; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public McpError? Error { get; init; }

    // ── factories ──────────────────────────────────────────────────────────

    /// <summary>Successful tool response with plain text content.</summary>
    public static McpResponse ToolSuccess(object? id, string text) => new()
    {
        Id     = id,
        Result = new
        {
            content = new[] { new { type = "text", text } },
            isError = false,
        },
    };

    /// <summary>Tool-level error (bad args, validation) — returned as content, not JSON-RPC error.</summary>
    public static McpResponse ToolError(object? id, string message) => new()
    {
        Id     = id,
        Result = new
        {
            content = new[] { new { type = "text", text = $"Error: {message}" } },
            isError = true,
        },
    };

    /// <summary>JSON-RPC protocol-level error.</summary>
    public static McpResponse ProtocolError(object? id, int code, string message) => new()
    {
        Id    = id,
        Error = new McpError(code, message),
    };

    // Standard JSON-RPC error codes
    public static McpResponse ParseError()          => ProtocolError(null, -32700, "Parse error");
    public static McpResponse InvalidRequest(object? id) => ProtocolError(id, -32600, "Invalid Request");
    public static McpResponse MethodNotFound(object? id, string method) =>
        ProtocolError(id, -32601, $"Method not found: {method}");
    public static McpResponse InvalidParams(object? id, string detail) =>
        ProtocolError(id, -32602, $"Invalid params: {detail}");
    public static McpResponse InternalError(object? id, string detail) =>
        ProtocolError(id, -32603, $"Internal error: {detail}");
}

public sealed record McpError(
    [property: JsonPropertyName("code")]    int    Code,
    [property: JsonPropertyName("message")] string Message);
