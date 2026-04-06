using System.Text.Json;
using DevMemory.Core.Interfaces;
using DevMemory.Mcp.Models;

namespace DevMemory.Mcp.Tools;

/// <summary>
/// artifact_list_project — list recent artifacts across all runs for a project.
/// </summary>
public sealed class ArtifactListProjectTool : IMcpTool
{
    private readonly IArtifactRepository _artifacts;

    public ArtifactListProjectTool(IArtifactRepository artifacts) => _artifacts = artifacts;

    public string Name        => "artifact_list_project";
    public string Description => "List recent artifacts across all runs for a project. Useful for reviewing prior work before starting a new run.";
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

        var artifacts = (await _artifacts.ListProjectArtifactsAsync(projectName, cancellationToken)).ToList();
        if (artifacts.Count == 0)
            return McpResponse.ToolSuccess(requestId, $"No artifacts found for project '{projectName}'");

        var lines = new System.Text.StringBuilder();
        lines.AppendLine($"Artifacts for project '{projectName}' ({artifacts.Count} total, latest version per run/type):");
        lines.AppendLine();

        var byRun = artifacts.GroupBy(a => a.RunId);
        foreach (var runGroup in byRun)
        {
            lines.AppendLine($"  Run: {runGroup.Key}");
            foreach (var a in runGroup)
            {
                lines.AppendLine($"    [{a.Type}] v{a.Version} — {a.CreatedAt:yyyy-MM-dd HH:mm}z");
                if (a.Metadata?.TryGetValue("goal", out var goal) == true)
                    lines.AppendLine($"      Goal: {goal}");
            }
            lines.AppendLine();
        }

        return McpResponse.ToolSuccess(requestId, lines.ToString().TrimEnd());
    }
}
