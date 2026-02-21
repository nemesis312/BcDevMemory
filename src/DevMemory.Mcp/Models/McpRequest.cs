using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevMemory.Mcp.Models;

/// <summary>
/// JSON-RPC 2.0 request. The id field is a JsonElement to handle
/// string, number, or null without losing precision.
/// </summary>
public sealed class McpRequest
{
    [JsonPropertyName("jsonrpc")] public string       JsonRpc { get; set; } = "2.0";
    [JsonPropertyName("id")]      public JsonElement? Id      { get; set; }
    [JsonPropertyName("method")]  public string       Method  { get; set; } = string.Empty;
    [JsonPropertyName("params")]  public JsonElement? Params  { get; set; }

    /// <summary>True when no id is present — notifications require no response.</summary>
    public bool IsNotification => Id is null;
}
