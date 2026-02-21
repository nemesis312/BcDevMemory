using System.Text.Json;
using DevMemory.Core.Enums;
using DevMemory.Core.Interfaces;
using DevMemory.Core.Utilities;
using DevMemory.Mcp.Models;

namespace DevMemory.Mcp.Tools;

/// <summary>
/// mem_save — persist a structured observation (bugfix, pattern, decision, etc.)
/// Auto-creates or reuses the active session for the given project.
/// </summary>
public sealed class MemSaveTool : IMcpTool
{
    private readonly IMemoryRepository  _memory;
    private readonly ISessionRepository _sessions;

    public MemSaveTool(IMemoryRepository memory, ISessionRepository sessions)
    {
        _memory   = memory;
        _sessions = sessions;
    }

    public string Name        => "mem_save";
    public string Description => "Save a structured observation (decision, bugfix, pattern, idea, etc.) to persistent memory.";
    public object InputSchema => new
    {
        type       = "object",
        properties = new
        {
            title   = new { type = "string", description = "Short, descriptive title" },
            type    = new { type = "string", description = $"Observation type. Valid values: {string.Join(", ", ObservationType.All)}" },
            content = new { type = "string", description = "Full content in What/Why/Where/Learned format" },
            project = new { type = "string", description = "Project name (optional)" },
            tags    = new { type = "array", items = new { type = "string" }, description = "Optional tags" },
        },
        required = new[] { "title", "type", "content" },
    };

    public async Task<McpResponse> ExecuteAsync(
        object? requestId, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (!arguments.TryGetProperty("title",   out var titleEl)   || titleEl.GetString()   is not { Length: > 0 } title)
            return McpResponse.InvalidParams(requestId, "'title' is required");
        if (!arguments.TryGetProperty("type",    out var typeEl)    || typeEl.GetString()    is not { Length: > 0 } type)
            return McpResponse.InvalidParams(requestId, "'type' is required");
        if (!arguments.TryGetProperty("content", out var contentEl) || contentEl.GetString() is not { Length: > 0 } content)
            return McpResponse.InvalidParams(requestId, "'content' is required");

        if (!ObservationType.IsValid(type))
            return McpResponse.ToolError(requestId,
                $"Unknown type '{type}'. Valid values: {string.Join(", ", ObservationType.All)}");

        var project = arguments.TryGetProperty("project", out var pEl) ? pEl.GetString() : null;
        var tags    = arguments.TryGetProperty("tags",    out var tEl) && tEl.ValueKind == JsonValueKind.Array
            ? tEl.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => s.Length > 0).ToArray()
            : null;

        // Get or create the active session for this project
        var session = await _sessions.GetActiveSessionAsync(project, cancellationToken)
                   ?? await _sessions.CreateSessionAsync(project, cancellationToken: cancellationToken);

        // Privacy stripping is also done in the repository, but applying here
        // catches it before session association logic as a defense-in-depth.
        content = PrivacyHelper.StripPrivateTags(content);

        var obs = await _memory.SaveObservationAsync(
            session.Id, title, type, content, project, tags, cancellationToken);

        var idShort = obs.Id.ToString("N")[..8];
        return McpResponse.ToolSuccess(requestId,
            $"✓ Saved: {title} [{type}] (ID: {idShort}...)");
    }
}
