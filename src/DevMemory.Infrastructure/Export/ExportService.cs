using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevMemory.Core.Interfaces;
using DevMemory.Core.Models;

namespace DevMemory.Infrastructure.Export;

/// <summary>
/// Exports observations to JSON or Markdown and imports from JSON.
/// </summary>
public sealed class ExportService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented          = true,
        PropertyNamingPolicy   = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IMemoryRepository _memory;
    private readonly ISessionRepository _sessions;

    public ExportService(IMemoryRepository memory, ISessionRepository sessions)
    {
        _memory   = memory;
        _sessions = sessions;
    }

    /// <summary>
    /// Exports observations to a JSON string.
    /// </summary>
    public async Task<string> ExportJsonAsync(
        string? project = null,
        int limit = 1000,
        CancellationToken cancellationToken = default)
    {
        var observations = await _memory.GetRecentObservationsAsync(project, limit, cancellationToken);

        var data = new ExportData
        {
            ExportedAt = DateTime.UtcNow,
            Project    = project,
            Observations = observations.Select(o => new ExportedObservation
            {
                Id        = o.Id,
                SessionId = o.SessionId,
                Title     = o.Title,
                Type      = o.Type,
                Content   = o.Content,
                Project   = o.Project,
                Tags      = o.Tags,
                CreatedAt = o.CreatedAt,
            }).ToList(),
        };

        return JsonSerializer.Serialize(data, JsonOpts);
    }

    /// <summary>
    /// Exports observations to a Markdown string.
    /// </summary>
    public async Task<string> ExportMarkdownAsync(
        string? project = null,
        int limit = 1000,
        CancellationToken cancellationToken = default)
    {
        var observations = (await _memory.GetRecentObservationsAsync(project, limit, cancellationToken)).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("# DevMemory Export");
        sb.AppendLine();

        if (project != null)
        {
            sb.AppendLine($"**Project:** {project}");
            sb.AppendLine();
        }

        sb.AppendLine($"**Exported:** {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC  ");
        sb.AppendLine($"**Observations:** {observations.Count}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        foreach (var obs in observations)
        {
            sb.AppendLine($"## [{obs.Type}] {obs.Title}");
            sb.AppendLine();
            sb.AppendLine($"**Date:** {obs.CreatedAt:yyyy-MM-dd HH:mm}  ");
            if (obs.Project != null)
                sb.AppendLine($"**Project:** {obs.Project}  ");
            if (obs.Tags.Length > 0)
                sb.AppendLine($"**Tags:** {string.Join(", ", obs.Tags)}  ");
            sb.AppendLine($"**ID:** `{obs.Id:N}`");
            sb.AppendLine();
            sb.AppendLine(obs.Content);
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Imports observations from a JSON export, creating a new session per distinct project.
    /// </summary>
    public async Task<int> ImportJsonAsync(
        string json,
        CancellationToken cancellationToken = default)
    {
        var data = JsonSerializer.Deserialize<ExportData>(json, JsonOpts)
            ?? throw new InvalidOperationException("Invalid export JSON.");

        // Group by project and get/create one session per project
        var sessionCache = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        async Task<Guid> GetOrCreateSession(string? proj)
        {
            var key = proj ?? string.Empty;
            if (sessionCache.TryGetValue(key, out var id)) return id;
            var session = await _sessions.CreateSessionAsync(proj, "Imported from DevMemory export",
                cancellationToken);
            sessionCache[key] = session.Id;
            return session.Id;
        }

        var count = 0;
        foreach (var obs in data.Observations)
        {
            var sessionId = await GetOrCreateSession(obs.Project);
            await _memory.SaveObservationAsync(
                sessionId, obs.Title, obs.Type, obs.Content,
                obs.Project, obs.Tags, cancellationToken);
            count++;
        }

        return count;
    }
}
