using System.Text.Json;
using DevMemory.Core.Interfaces;
using DevMemory.Mcp.Models;

namespace DevMemory.Mcp.Tools;

public sealed class MemSessionEndTool : IMcpTool
{
    private readonly ISessionRepository _sessions;

    public MemSessionEndTool(ISessionRepository sessions) => _sessions = sessions;

    public string Name        => "mem_session_end";
    public string Description => "End the current active session, optionally saving a final summary.";
    public object InputSchema => new
    {
        type       = "object",
        properties = new
        {
            summary    = new { type = "string", description = "Final summary of what was accomplished" },
            project    = new { type = "string", description = "Project name (to locate the active session)" },
            session_id = new { type = "string", description = "Specific session ID to end (optional)" },
        },
    };

    public async Task<McpResponse> ExecuteAsync(
        object? requestId, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        var summary = arguments.TryGetProperty("summary",    out var sEl) ? sEl.GetString() : null;
        var project = arguments.TryGetProperty("project",    out var pEl) ? pEl.GetString() : null;

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
                return McpResponse.ToolError(requestId, "No active session found.");
            sessionId = session.Id;
        }

        await _sessions.EndSessionAsync(sessionId, summary, cancellationToken);

        return McpResponse.ToolSuccess(requestId, "✓ Session ended.");
    }
}
