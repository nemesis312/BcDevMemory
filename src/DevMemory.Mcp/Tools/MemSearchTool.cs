using System.Text;
using System.Text.Json;
using DevMemory.Core.Interfaces;
using DevMemory.Mcp.Models;

namespace DevMemory.Mcp.Tools;

public sealed class MemSearchTool : IMcpTool
{
    private readonly ISearchService _search;

    public MemSearchTool(ISearchService search) => _search = search;

    public string Name        => "mem_search";
    public string Description => "Full-text search across saved observations. Returns ranked results with previews.";
    public object InputSchema => new
    {
        type       = "object",
        properties = new
        {
            query   = new { type = "string", description = "Search query (supports natural language)" },
            project = new { type = "string", description = "Filter to a specific project (optional)" },
            limit   = new { type = "integer", description = "Max results to return (default 10)" },
        },
        required = new[] { "query" },
    };

    public async Task<McpResponse> ExecuteAsync(
        object? requestId, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (!arguments.TryGetProperty("query", out var qEl) || qEl.GetString() is not { Length: > 0 } query)
            return McpResponse.InvalidParams(requestId, "'query' is required");

        var project = arguments.TryGetProperty("project", out var pEl) ? pEl.GetString() : null;
        var limit   = arguments.TryGetProperty("limit",   out var lEl) && lEl.TryGetInt32(out var l) ? l : 10;

        var results = (await _search.SearchAsync(query, project, limit, cancellationToken: cancellationToken)).ToList();

        if (results.Count == 0)
            return McpResponse.ToolSuccess(requestId, $"No results found for \"{query}\".");

        var sb = new StringBuilder();
        sb.AppendLine($"Found {results.Count} result(s) for \"{query}\":");
        sb.AppendLine();

        foreach (var (obs, i) in results.Select((o, i) => (o, i + 1)))
        {
            sb.AppendLine($"{i}. [{obs.Type}] {obs.Title}");
            if (obs.Project != null) sb.AppendLine($"   Project: {obs.Project}  |  {obs.CreatedAt:yyyy-MM-dd}  |  Rank: {obs.Rank:F2}");
            if (!string.IsNullOrEmpty(obs.ContentPreview))
                sb.AppendLine($"   {obs.ContentPreview.Replace("\n", " ")}");
            sb.AppendLine($"   ID: {obs.Id:N}");
            sb.AppendLine();
        }

        return McpResponse.ToolSuccess(requestId, sb.ToString().TrimEnd());
    }
}
