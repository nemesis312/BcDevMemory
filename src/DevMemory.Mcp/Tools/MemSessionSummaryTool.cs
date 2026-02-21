using System.Text.Json;
using DevMemory.Core.Interfaces;
using DevMemory.Mcp.Models;

namespace DevMemory.Mcp.Tools;

/// <summary>
/// mem_session_summary — save a summary on the active session at the end of a work session.
/// </summary>
public sealed class MemSessionSummaryTool : IMcpTool
{
    private readonly ISessionRepository _sessions;

    public MemSessionSummaryTool(ISessionRepository sessions) => _sessions = sessions;

    public string Name        => "mem_session_summary";
    public string Description => "Save a summary of what was accomplished in the current session.";
    public object InputSchema => new
    {
        type       = "object",
        properties = new
        {
            summary       = new { type = "string", description = "Summary of what was done this session" },
            project       = new { type = "string", description = "Project name (to find the active session)" },
            files_changed = new { type = "array",  items = new { type = "string" }, description = "Files modified" },
            session_id    = new { type = "string", description = "Specific session ID (optional, overrides project lookup)" },
        },
        required = new[] { "summary" },
    };

    public async Task<McpResponse> ExecuteAsync(
        object? requestId, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (!arguments.TryGetProperty("summary", out var sEl) || sEl.GetString() is not { Length: > 0 } summary)
            return McpResponse.InvalidParams(requestId, "'summary' is required");

        var project  = arguments.TryGetProperty("project",    out var pEl) ? pEl.GetString() : null;
        var files    = arguments.TryGetProperty("files_changed", out var fEl) && fEl.ValueKind == JsonValueKind.Array
            ? fEl.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => s.Length > 0).ToArray()
            : null;

        Guid sessionId;

        if (arguments.TryGetProperty("session_id", out var idEl) &&
            Guid.TryParse(idEl.GetString(), out var explicitId))
        {
            sessionId = explicitId;
        }
        else
        {
            var session = await _sessions.GetActiveSessionAsync(project, cancellationToken);
            if (session is null)
                return McpResponse.ToolError(requestId,
                    "No active session found. Start one with mem_session_start.");
            sessionId = session.Id;
        }

        await _sessions.UpdateSessionAsync(sessionId, summary, files, cancellationToken);

        return McpResponse.ToolSuccess(requestId,
            $"✓ Session summary saved. Files changed: {files?.Length ?? 0}.");
    }
}
