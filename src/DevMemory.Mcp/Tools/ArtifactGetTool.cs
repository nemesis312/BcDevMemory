using System.Text.Json;
using DevMemory.Core.Interfaces;
using DevMemory.Mcp.Models;

namespace DevMemory.Mcp.Tools;

/// <summary>
/// artifact_get — retrieve the latest version of a specific artifact for a run.
/// </summary>
public sealed class ArtifactGetTool : IMcpTool
{
    private readonly IArtifactRepository _artifacts;

    public ArtifactGetTool(IArtifactRepository artifacts) => _artifacts = artifacts;

    public string Name        => "artifact_get";
    public string Description => "Retrieve the latest version of a specific artifact (prd, design, plan, etc.) for a bc-agentic run.";
    public bool   IsReadOnly  => true;
    public object InputSchema => new
    {
        type       = "object",
        properties = new
        {
            run_id        = new { type = "string", description = "The bc-agentic run ID" },
            artifact_type = new { type = "string", description = "Artifact type: prd, design, plan, report, spec, exploration, tasks, verify" },
        },
        required = new[] { "run_id", "artifact_type" },
    };

    public async Task<McpResponse> ExecuteAsync(
        object? requestId, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (!arguments.TryGetProperty("run_id",        out var runEl)  || runEl.GetString()  is not { Length: > 0 } runId)
            return McpResponse.InvalidParams(requestId, "'run_id' is required");
        if (!arguments.TryGetProperty("artifact_type", out var typeEl) || typeEl.GetString() is not { Length: > 0 } artifactType)
            return McpResponse.InvalidParams(requestId, "'artifact_type' is required");

        var artifact = await _artifacts.GetLatestArtifactAsync(runId, artifactType, cancellationToken);
        if (artifact is null)
            return McpResponse.ToolError(requestId,
                $"No artifact of type '{artifactType}' found for run '{runId}'");

        var meta = artifact.Metadata is null ? "" :
            string.Join(", ", artifact.Metadata.Select(kv => $"{kv.Key}: {kv.Value}"));

        return McpResponse.ToolSuccess(requestId,
            $"[{artifact.Type}] v{artifact.Version} — {artifact.ProjectName} / {artifact.RunId}\n" +
            (meta.Length > 0 ? $"Metadata: {meta}\n" : "") +
            $"Created: {artifact.CreatedAt:O}\n\n" +
            artifact.Content);
    }
}
