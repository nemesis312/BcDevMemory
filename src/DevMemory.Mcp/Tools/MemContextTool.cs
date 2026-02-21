using System.Text;
using System.Text.Json;
using DevMemory.Core.Interfaces;
using DevMemory.Mcp.Models;

namespace DevMemory.Mcp.Tools;

/// <summary>
/// mem_context — returns recent session history for a project so the AI
/// can quickly understand what was done in past sessions.
/// Also detects and reports stale (orphaned) active sessions.
/// </summary>
public sealed class MemContextTool : IMcpTool
{
    private readonly ISessionRepository _sessions;

    /// <summary>
    /// Sessions older than this are considered stale/orphaned from a previous context.
    /// </summary>
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromHours(8);

    public MemContextTool(ISessionRepository sessions) => _sessions = sessions;

    public string Name => "mem_context";
    public string Description => "Load recent session context for a project. Detects stale sessions from previous contexts. Call this at session start to restore memory.";
    public object InputSchema => new
    {
        type = "object",
        properties = new
        {
            project = new { type = "string", description = "Project name to load context for" },
            limit = new { type = "integer", description = "Max sessions to return (default 5)" },
            auto_close = new { type = "boolean", description = "Auto-close stale sessions (default true)" },
        },
        required = new[] { "project" },
    };

    public async Task<McpResponse> ExecuteAsync(
        object? requestId, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (!arguments.TryGetProperty("project", out var pEl) || pEl.GetString() is not { Length: > 0 } project)
            return McpResponse.InvalidParams(requestId, "'project' is required");

        var limit = arguments.TryGetProperty("limit", out var lEl) && lEl.TryGetInt32(out var l) ? l : 5;
        var autoClose = !arguments.TryGetProperty("auto_close", out var acEl) || acEl.GetBoolean();

        var sb = new StringBuilder();

        // ── Detect stale/orphaned sessions ────────────────────────────────────
        var staleSession = await _sessions.GetStaleActiveSessionAsync(project, StaleThreshold, cancellationToken);
        if (staleSession != null)
        {
            var age = DateTime.UtcNow - staleSession.StartedAt;
            sb.AppendLine("⚠️  ORPHANED SESSION DETECTED");
            sb.AppendLine($"   Session ID: {staleSession.Id:N}");
            sb.AppendLine($"   Started: {staleSession.StartedAt:yyyy-MM-dd HH:mm} UTC ({age.TotalHours:F1}h ago)");
            sb.AppendLine($"   Goal: {staleSession.Goal ?? "(none)"}");

            if (autoClose)
            {
                await _sessions.EndSessionAsync(staleSession.Id,
                    "[Auto-closed by mem_context: detected as stale after context reset]",
                    cancellationToken);
                sb.AppendLine("   → Session auto-closed. Starting fresh.");
            }
            else
            {
                sb.AppendLine("   → Use mem_session_end to close it, or mem_session_start to auto-close and start fresh.");
            }
            sb.AppendLine();
        }

        // ── Load completed session history ────────────────────────────────────
        var ctx = await _sessions.GetProjectContextAsync(project, limit, cancellationToken);

        if (ctx.RecentSessions.Count == 0 && staleSession == null)
            return McpResponse.ToolSuccess(requestId, $"No session history found for project '{project}'.");

        sb.AppendLine($"Context for project: {project}");
        sb.AppendLine($"Completed sessions: {ctx.RecentSessions.Count}");
        sb.AppendLine();

        foreach (var session in ctx.RecentSessions)
        {
            var start = session.StartedAt.ToString("yyyy-MM-dd HH:mm");
            var end = session.EndedAt?.ToString("yyyy-MM-dd HH:mm") ?? "active";
            sb.AppendLine($"── Session {start} → {end} ({session.ObservationCount} observations)");

            if (!string.IsNullOrEmpty(session.SessionGoal))
                sb.AppendLine($"   Goal:    {session.SessionGoal}");
            if (!string.IsNullOrEmpty(session.SessionSummary))
                sb.AppendLine($"   Summary: {session.SessionSummary}");

            foreach (var obs in session.RecentObservations.Take(5))
                sb.AppendLine($"   • [{obs.Type}] {obs.Title}  ({obs.CreatedAt:HH:mm})");

            if (session.ObservationCount > 5)
                sb.AppendLine($"   … and {session.ObservationCount - 5} more. Use mem_search to find specific items.");

            sb.AppendLine();
        }

        return McpResponse.ToolSuccess(requestId, sb.ToString().TrimEnd());
    }
}
