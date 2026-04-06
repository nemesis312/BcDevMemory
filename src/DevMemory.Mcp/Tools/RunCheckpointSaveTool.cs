using System.Text.Json;
using DevMemory.Core.Interfaces;
using DevMemory.Mcp.Models;

namespace DevMemory.Mcp.Tools;

/// <summary>
/// run_checkpoint_save — persist the current state of a bc-agentic run.
/// Upserts on run_id — calling again updates the existing checkpoint.
/// </summary>
public sealed class RunCheckpointSaveTool : IMcpTool
{
    private readonly IRunStateRepository _runStates;

    public RunCheckpointSaveTool(IRunStateRepository runStates) => _runStates = runStates;

    public string Name        => "run_checkpoint_save";
    public string Description => "Persist the current state of a bc-agentic run (phase, approvals, mode). Upserts on run_id.";
    public object InputSchema => new
    {
        type       = "object",
        properties = new
        {
            run_id                = new { type = "string", description = "The bc-agentic run ID" },
            project_name          = new { type = "string", description = "Project name" },
            goal                  = new { type = "string", description = "Run goal" },
            current_phase         = new { type = "string", description = "Current phase: prd, design, plan, execute, done, etc." },
            approvals             = new { type = "object", description = "Approval state as JSON object: { \"prd\": true, \"design\": false, \"plan\": false }" },
            last_handoff_summary  = new { type = "string", description = "Summary from the last subagent handoff (optional)" },
            open_risks            = new { type = "array",  items = new { type = "string" }, description = "List of open risks or blockers (optional)" },
            mode                  = new { type = "string", description = "Execution mode: safe, apply, or god (default: safe)" },
        },
        required = new[] { "run_id", "project_name", "goal", "current_phase" },
    };

    public async Task<McpResponse> ExecuteAsync(
        object? requestId, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (!arguments.TryGetProperty("run_id",       out var runEl)   || runEl.GetString()   is not { Length: > 0 } runId)
            return McpResponse.InvalidParams(requestId, "'run_id' is required");
        if (!arguments.TryGetProperty("project_name", out var projEl)  || projEl.GetString()  is not { Length: > 0 } projectName)
            return McpResponse.InvalidParams(requestId, "'project_name' is required");
        if (!arguments.TryGetProperty("goal",         out var goalEl)  || goalEl.GetString()  is not { Length: > 0 } goal)
            return McpResponse.InvalidParams(requestId, "'goal' is required");
        if (!arguments.TryGetProperty("current_phase",out var phaseEl) || phaseEl.GetString() is not { Length: > 0 } currentPhase)
            return McpResponse.InvalidParams(requestId, "'current_phase' is required");

        var approvals = new Dictionary<string, bool>();
        if (arguments.TryGetProperty("approvals", out var appEl) && appEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in appEl.EnumerateObject())
                approvals[prop.Name] = prop.Value.ValueKind == JsonValueKind.True;
        }

        var lastHandoff = arguments.TryGetProperty("last_handoff_summary", out var hsEl)
            ? hsEl.GetString()
            : null;

        var openRisks = Array.Empty<string>();
        if (arguments.TryGetProperty("open_risks", out var risksEl) && risksEl.ValueKind == JsonValueKind.Array)
            openRisks = risksEl.EnumerateArray()
                .Select(e => e.GetString() ?? "")
                .Where(s => s.Length > 0)
                .ToArray();

        var mode = arguments.TryGetProperty("mode", out var modeEl) && modeEl.GetString() is { Length: > 0 } m
            ? m
            : "safe";

        await _runStates.SaveCheckpointAsync(
            runId, projectName, goal, currentPhase,
            approvals, lastHandoff, openRisks, mode,
            cancellationToken);

        return McpResponse.ToolSuccess(requestId,
            $"✓ Checkpoint saved: run {runId} — phase={currentPhase}, mode={mode}");
    }
}
