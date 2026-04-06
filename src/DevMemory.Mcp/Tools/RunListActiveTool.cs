using System.Text.Json;
using DevMemory.Core.Interfaces;
using DevMemory.Mcp.Models;

namespace DevMemory.Mcp.Tools;

/// <summary>
/// run_list_active — list active (non-completed) runs for a project.
/// </summary>
public sealed class RunListActiveTool : IMcpTool
{
    private readonly IRunStateRepository _runStates;

    public RunListActiveTool(IRunStateRepository runStates) => _runStates = runStates;

    public string Name        => "run_list_active";
    public string Description => "List active (non-completed) bc-agentic runs for a project.";
    public bool   IsReadOnly  => true;
    public object InputSchema => new
    {
        type       = "object",
        properties = new
        {
            project_name = new { type = "string", description = "Project name" },
        },
        required = new[] { "project_name" },
    };

    public async Task<McpResponse> ExecuteAsync(
        object? requestId, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (!arguments.TryGetProperty("project_name", out var projEl) || projEl.GetString() is not { Length: > 0 } projectName)
            return McpResponse.InvalidParams(requestId, "'project_name' is required");

        var runs = (await _runStates.ListActiveRunsAsync(projectName, cancellationToken)).ToList();
        if (runs.Count == 0)
            return McpResponse.ToolSuccess(requestId, $"No active runs found for project '{projectName}'");

        var lines = new System.Text.StringBuilder();
        lines.AppendLine($"Active runs for '{projectName}' ({runs.Count}):");
        lines.AppendLine();
        foreach (var r in runs)
        {
            var approved = r.Approvals.Count(kv => kv.Value);
            lines.AppendLine($"  {r.RunId}");
            lines.AppendLine($"    Goal:    {r.Goal}");
            lines.AppendLine($"    Phase:   {r.CurrentPhase}  |  Mode: {r.Mode}");
            lines.AppendLine($"    Approvals: {approved}/{r.Approvals.Count}");
            lines.AppendLine($"    Updated: {r.UpdatedAt:yyyy-MM-dd HH:mm}z");
            lines.AppendLine();
        }

        return McpResponse.ToolSuccess(requestId, lines.ToString().TrimEnd());
    }
}
