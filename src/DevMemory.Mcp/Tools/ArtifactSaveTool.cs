using System.Text.Json;
using DevMemory.Core.Interfaces;
using DevMemory.Mcp.Models;

namespace DevMemory.Mcp.Tools;

/// <summary>
/// artifact_save — persist a bc-agentic-os run artifact (prd, design, plan, report, etc.)
/// Artifacts are immutable: saving again creates a new version.
/// </summary>
public sealed class ArtifactSaveTool : IMcpTool
{
    private static readonly HashSet<string> ValidTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "prd", "design", "plan", "report", "spec", "exploration", "tasks", "verify"
    };

    private readonly IArtifactRepository _artifacts;

    public ArtifactSaveTool(IArtifactRepository artifacts) => _artifacts = artifacts;

    public string Name        => "artifact_save";
    public string Description => "Save a bc-agentic-os run artifact (prd, design, plan, report, spec, exploration, tasks, verify) to persistent memory. Artifacts are immutable — re-saving creates a new version.";
    public object InputSchema => new
    {
        type       = "object",
        properties = new
        {
            run_id       = new { type = "string", description = "The bc-agentic run ID" },
            project_name = new { type = "string", description = "Project name" },
            artifact_type = new { type = "string", description = $"Artifact type. Valid values: {string.Join(", ", ValidTypes.OrderBy(t => t))}" },
            content      = new { type = "string", description = "Full markdown content of the artifact" },
            goal         = new { type = "string", description = "Run goal (optional metadata)" },
            agent_role   = new { type = "string", description = "Agent role that produced this artifact (optional)" },
        },
        required = new[] { "run_id", "project_name", "artifact_type", "content" },
    };

    public async Task<McpResponse> ExecuteAsync(
        object? requestId, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (!arguments.TryGetProperty("run_id",       out var runEl)  || runEl.GetString()  is not { Length: > 0 } runId)
            return McpResponse.InvalidParams(requestId, "'run_id' is required");
        if (!arguments.TryGetProperty("project_name", out var projEl) || projEl.GetString() is not { Length: > 0 } projectName)
            return McpResponse.InvalidParams(requestId, "'project_name' is required");
        if (!arguments.TryGetProperty("artifact_type", out var typeEl) || typeEl.GetString() is not { Length: > 0 } artifactType)
            return McpResponse.InvalidParams(requestId, "'artifact_type' is required");
        if (!arguments.TryGetProperty("content",      out var contentEl) || contentEl.GetString() is not { Length: > 0 } content)
            return McpResponse.InvalidParams(requestId, "'content' is required");

        if (!ValidTypes.Contains(artifactType))
            return McpResponse.ToolError(requestId,
                $"Unknown artifact_type '{artifactType}'. Valid values: {string.Join(", ", ValidTypes.OrderBy(t => t))}");

        var metadata = new Dictionary<string, string>();
        if (arguments.TryGetProperty("goal",       out var goalEl)  && goalEl.GetString()  is { Length: > 0 } goal)
            metadata["goal"] = goal;
        if (arguments.TryGetProperty("agent_role", out var roleEl)  && roleEl.GetString()  is { Length: > 0 } role)
            metadata["agentRole"] = role;

        var artifact = await _artifacts.SaveArtifactAsync(
            runId, projectName, artifactType.ToLowerInvariant(), content,
            metadata.Count > 0 ? metadata : null,
            cancellationToken);

        return McpResponse.ToolSuccess(requestId,
            $"✓ Artifact saved: [{artifact.Type}] v{artifact.Version} for run {runId} (ID: {artifact.Id[..8]}...)");
    }
}
