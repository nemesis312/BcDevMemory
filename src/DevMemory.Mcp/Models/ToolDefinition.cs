using System.Text.Json.Serialization;

namespace DevMemory.Mcp.Models;

/// <summary>
/// Wire format for a single tool in the tools/list response.
/// </summary>
public sealed class ToolDefinition
{
    [JsonPropertyName("name")]        public string  Name        { get; init; } = string.Empty;
    [JsonPropertyName("description")] public string  Description { get; init; } = string.Empty;
    [JsonPropertyName("inputSchema")] public object  InputSchema { get; init; } = new { type = "object" };

    [JsonPropertyName("annotations")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Annotations { get; init; }
}
