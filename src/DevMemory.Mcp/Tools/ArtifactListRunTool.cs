using System.Text.Json;
using DevMemory.Core.Interfaces;
using DevMemory.Mcp.Models;

namespace DevMemory.Mcp.Tools;

/// <summary>
/// artifact_list_run — list all artifacts (latest version per type) for a run.
/// </summary>
public sealed class ArtifactListRunTool : IMcpTool
{
    private readonly IArtifactRepository _artifacts;

    public ArtifactListRunTool(IArtifactRepository artifacts) => _artifacts = artifacts;

    public string Name        => "artifact_list_run";
    public string Description => "List all artifacts (latest version per type) for a bc-agentic run.";
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

        var artifacts = (await _artifacts.ListRunArtifactsAsync(runId, cancellationToken)).ToList();
        if (artifacts.Count == 0)
            return McpResponse.ToolSuccess(requestId, $"No artifacts found for run '{runId}'");

        var lines = new System.Text.StringBuilder();
        lines.AppendLine($"Artifacts for run {runId} ({artifacts.Count} total):");
        lines.AppendLine();
        foreach (var a in artifacts)
        {
            lines.AppendLine($"  [{a.Type}] v{a.Version} — {a.CreatedAt:yyyy-MM-dd HH:mm}z  (ID: {a.Id[..8]}...)");
            if (a.Metadata?.TryGetValue("goal", out var goal) == true)
                lines.AppendLine($"    Goal: {goal}");
            if (a.Metadata?.TryGetValue("agentRole", out var role) == true)
                lines.AppendLine($"    Agent: {role}");
        }

        return McpResponse.ToolSuccess(requestId, lines.ToString().TrimEnd());
    }
}
