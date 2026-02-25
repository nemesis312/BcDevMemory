using System.Text.Json;
using System.Text.Json.Serialization;
using DevMemory.Mcp.Models;
using DevMemory.Mcp.Tools;

namespace DevMemory.Mcp.Server;

/// <summary>
/// Routes JSON-RPC 2.0 messages to the correct MCP handler.
/// Handles: initialize, initialized, ping, tools/list, tools/call.
/// </summary>
public sealed class JsonRpcHandler
{

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    private readonly Dictionary<string, IMcpTool> _tools;
    private readonly ToolDefinition[]             _toolList;

    public JsonRpcHandler(IEnumerable<IMcpTool> tools)
    {
        _tools    = tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        _toolList = _tools.Values.Select(t => new ToolDefinition
        {
            Name        = t.Name,
            Description = t.Description,
            InputSchema = t.InputSchema,
            Annotations = t.IsReadOnly ? new { readOnlyHint = true } : null,
        }).ToArray();
    }

    public async Task<string?> HandleAsync(string json, CancellationToken cancellationToken)
    {
        McpRequest request;
        try
        {
            request = JsonSerializer.Deserialize<McpRequest>(json, JsonOpts)
                   ?? throw new JsonException("Null message");
        }
        catch
        {
            // Don't send ParseError with id:null — MCP clients reject null ids.
            await Console.Error.WriteLineAsync("[devmemory] Failed to parse incoming JSON-RPC message");
            return null;
        }

        // Extract the raw id value to echo back in responses
        object? id = request.Id?.ValueKind switch
        {
            JsonValueKind.Number => request.Id.Value.GetInt64(),
            JsonValueKind.String => request.Id.Value.GetString(),
            _                    => null,
        };

        // Notifications have no id — never send a response for them.
        if (request.IsNotification)
        {
            return null;
        }

        try
        {
            var response = request.Method switch
            {
                "initialize"  => HandleInitialize(id),
                "ping"        => HandlePing(id),
                "tools/list"  => HandleToolsList(id),
                "tools/call"  => await HandleToolCallAsync(id, request, cancellationToken),
                _             => McpResponse.MethodNotFound(id, request.Method),
            };

            return response is null ? null : Serialize(response);
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"[devmemory] Error handling {request.Method}: {ex}");
            return Serialize(McpResponse.InternalError(id, ex.Message));
        }
    }

    public string SerializeInternalError(object? id, string message) =>
        Serialize(McpResponse.InternalError(id, message));

    // ── handlers ───────────────────────────────────────────────────────────

    private static McpResponse HandleInitialize(object? id) => new()
    {
        Id = id,
        Result = new
        {
            protocolVersion = McpConstants.ProtocolVersion,
            capabilities    = new { tools = new { listChanged = false } },
            serverInfo      = new { name = McpConstants.ServerName, version = McpConstants.ServerVersion },
        },
    };

    private static McpResponse HandlePing(object? id) => new()
    {
        Id     = id,
        Result = new { },
    };

    private McpResponse HandleToolsList(object? id) => new()
    {
        Id     = id,
        Result = new { tools = _toolList },
    };

    private async Task<McpResponse> HandleToolCallAsync(
        object? id, McpRequest request, CancellationToken cancellationToken)
    {
        var paramsEl = request.Params;
        if (paramsEl is null)
            return McpResponse.InvalidParams(id, "Missing params");

        if (!paramsEl.Value.TryGetProperty("name", out var nameEl) ||
            nameEl.GetString() is not { } toolName)
            return McpResponse.InvalidParams(id, "Missing tool name");

        if (!_tools.TryGetValue(toolName, out var tool))
            return McpResponse.MethodNotFound(id, $"Tool '{toolName}'");

        var arguments = paramsEl.Value.TryGetProperty("arguments", out var argEl)
            ? argEl
            : default;

        return await tool.ExecuteAsync(id, arguments, cancellationToken);
    }

    // ── serialization ──────────────────────────────────────────────────────

    private static string Serialize(McpResponse response) =>
        JsonSerializer.Serialize(response, JsonOpts);
}
