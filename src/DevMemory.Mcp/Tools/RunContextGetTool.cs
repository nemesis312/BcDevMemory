using System.Text.Json;
using DevMemory.Core.Interfaces;
using DevMemory.Mcp.Models;

namespace DevMemory.Mcp.Tools;

/// <summary>
/// run_context_get — retrieve the full context of a bc-agentic run:
/// current state checkpoint + list of available artifacts.
/// </summary>
public sealed class RunContextGetTool : IMcpTool
{
    private readonly IRunStateRepository  _runStates;
    private readonly IArtifactRepository  _artifacts;

    public RunContextGetTool(IRunStateRepository runStates, IArtifactRepository artifacts)
    {
        _runStates = runStates;
        _artifacts = artifacts;
    }

    public string Name        => "run_context_get";
    public string Description => "Get the full context of a bc-agentic run: current phase, approvals, mode, and available artifacts.";
    public bool   IsReadOnly  => true;
    public object InputSchema => new
    {
        type       = "object",
        properties = new
        {
            run_id = new { type = "string", description = "The bc-agentic run ID" },
        },
        required = new[] { "run_id" },
    };

    public async Task<McpResponse> ExecuteAsync(
        object? requestId, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (!arguments.TryGetProperty("run_id", out var runEl) || runEl.GetString() is not { Length: > 0 } runId)
            return McpResponse.InvalidParams(requestId, "'run_id' is required");

        var stateTask     = _runStates.GetRunStateAsync(runId, cancellationToken);
        var artifactsTask = _artifacts.ListRunArtifactsAsync(runId, cancellationToken);
        await Task.WhenAll(stateTask, artifactsTask);

        var state     = stateTask.Result;
        var artifacts = artifactsTask.Result.ToList();

        var lines = new System.Text.StringBuilder();

        if (state is null)
        {
            lines.AppendLine($"No checkpoint found for run '{runId}'.");
        }
        else
        {
            lines.AppendLine($"Run: {state.RunId}");
            lines.AppendLine($"Project: {state.ProjectName}");
            lines.AppendLine($"Goal: {state.Goal}");
            lines.AppendLine($"Phase: {state.CurrentPhase}");
            lines.AppendLine($"Mode: {state.Mode}");
            lines.AppendLine($"Updated: {state.UpdatedAt:yyyy-MM-dd HH:mm}z");
            lines.AppendLine();

            lines.AppendLine("Approvals:");
            foreach (var kv in state.Approvals)
                lines.AppendLine($"  {kv.Key}: {(kv.Value ? "✓ approved" : "pending")}");

            if (state.LastHandoffSummary is { Length: > 0 })
            {
                lines.AppendLine();
                lines.AppendLine($"Last handoff: {state.LastHandoffSummary}");
            }

            if (state.OpenRisks.Length > 0)
            {
                lines.AppendLine();
                lines.AppendLine("Open risks:");
                foreach (var risk in state.OpenRisks)
                    lines.AppendLine($"  - {risk}");
            }
        }

        lines.AppendLine();
        if (artifacts.Count == 0)
        {
            lines.AppendLine("Artifacts: none");
        }
        else
        {
            lines.AppendLine($"Artifacts ({artifacts.Count}):");
            foreach (var a in artifacts)
                lines.AppendLine($"  [{a.Type}] v{a.Version} — {a.CreatedAt:yyyy-MM-dd HH:mm}z");
        }

        return McpResponse.ToolSuccess(requestId, lines.ToString().TrimEnd());
    }
}
