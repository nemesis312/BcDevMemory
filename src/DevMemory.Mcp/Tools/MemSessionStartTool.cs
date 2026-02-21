using System.Text.Json;
using DevMemory.Core.Interfaces;
using DevMemory.Mcp.Models;

namespace DevMemory.Mcp.Tools;

public sealed class MemSessionStartTool : IMcpTool
{
    private readonly ISessionRepository _sessions;

    /// <summary>
    /// Sessions older than this threshold are considered stale and will be auto-closed.
    /// </summary>
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromHours(8);

    public MemSessionStartTool(ISessionRepository sessions) => _sessions = sessions;

    public string Name => "mem_session_start";
    public string Description => "Start a new coding session, optionally setting a goal. Auto-closes stale sessions (>8h old).";
    public object InputSchema => new
    {
        type = "object",
        properties = new
        {
            project = new { type = "string", description = "Project name" },
            goal = new { type = "string", description = "What you plan to accomplish this session" },
        },
    };

    public async Task<McpResponse> ExecuteAsync(
        object? requestId, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        var project = arguments.TryGetProperty("project", out var pEl) ? pEl.GetString() : null;
        var goal = arguments.TryGetProperty("goal", out var gEl) ? gEl.GetString() : null;

        // Auto-close any stale sessions before creating a new one
        var closedCount = await _sessions.CloseStaleSessionsAsync(StaleThreshold, cancellationToken);

        var session = await _sessions.CreateSessionAsync(project, goal, cancellationToken);

        var staleNote = closedCount > 0
            ? $"\n  (Auto-closed {closedCount} stale session(s) older than {StaleThreshold.TotalHours}h)"
            : "";

        return McpResponse.ToolSuccess(requestId,
            $"✓ Session started. ID: {session.Id:N}\n" +
            $"  Project: {project ?? "(none)"}\n" +
            $"  Goal: {goal ?? "(none)"}{staleNote}");
    }
}
