using System.ComponentModel;
using DevMemory.Core.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DevMemory.Cli.Commands;

/// <summary>
/// Starts the DevMemory HTTP API server (Minimal APIs) on the configured port.
/// Uses whatever storage backend is already configured for the CLI process
/// (SQLite, PostgreSQL, or Neo4j) — no hardcoded backend.
/// </summary>
internal sealed class ServeCommand : AsyncCommand<ServeCommand.Settings>
{
    private readonly IMemoryRepository  _memory;
    private readonly ISessionRepository _sessions;
    private readonly ISearchService     _search;

    public ServeCommand(
        IMemoryRepository  memory,
        ISessionRepository sessions,
        ISearchService     search)
    {
        _memory   = memory;
        _sessions = sessions;
        _search   = search;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("-p|--port <port>")]
        [Description("Port to listen on (default: 7437)")]
        [DefaultValue(7437)]
        public int Port { get; set; } = 7437;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();

        // Register the already-resolved storage instances so Minimal API endpoints
        // can inject them via DI — works with any configured backend.
        builder.Services.AddSingleton(_memory);
        builder.Services.AddSingleton(_sessions);
        builder.Services.AddSingleton(_search);

        builder.WebHost.UseUrls($"http://0.0.0.0:{settings.Port}");

        var app = builder.Build();

        // ── /observations ───────────────────────────────────────────────────
        app.MapGet("/observations", async (
            string? project, int limit,
            IMemoryRepository repo) =>
        {
            limit = limit <= 0 ? 20 : limit;
            var items = await repo.GetRecentObservationsAsync(project, limit);
            return Results.Ok(items);
        });

        app.MapGet("/observations/{id:guid}", async (
            Guid id, IMemoryRepository repo) =>
        {
            var obs = await repo.GetObservationAsync(id);
            return obs is null ? Results.NotFound() : Results.Ok(obs);
        });

        app.MapPost("/observations", async (
            SaveObservationRequest req,
            IMemoryRepository memory,
            ISessionRepository sessions) =>
        {
            var session = req.SessionId.HasValue
                ? await sessions.GetSessionAsync(req.SessionId.Value)
                : await sessions.GetActiveSessionAsync(req.Project)
                    ?? await sessions.CreateSessionAsync(req.Project);

            if (session is null)
                return Results.BadRequest("Session not found.");

            var obs = await memory.SaveObservationAsync(
                session.Id, req.Title, req.Type, req.Content,
                req.Project, req.Tags);

            return Results.Created($"/observations/{obs.Id}", obs);
        });

        // ── /search ─────────────────────────────────────────────────────────
        app.MapGet("/search", async (
            string q, string? project, string? type, int limit,
            ISearchService search) =>
        {
            if (string.IsNullOrWhiteSpace(q))
                return Results.BadRequest("Query parameter 'q' is required.");

            limit = limit <= 0 ? 10 : limit;
            var results = await search.SearchAsync(q, project, limit, type);
            return Results.Ok(results);
        });

        // ── /sessions ───────────────────────────────────────────────────────
        app.MapGet("/sessions", async (
            string? project, int limit,
            ISessionRepository sessions) =>
        {
            limit = limit <= 0 ? 5 : limit;
            var items = await sessions.GetRecentSessionsAsync(project, limit);
            return Results.Ok(items);
        });

        app.MapGet("/sessions/active", async (
            string? project, ISessionRepository sessions) =>
        {
            var session = await sessions.GetActiveSessionAsync(project);
            return session is null ? Results.NotFound() : Results.Ok(session);
        });

        app.MapPost("/sessions", async (
            CreateSessionRequest req, ISessionRepository sessions) =>
        {
            var session = await sessions.CreateSessionAsync(req.Project, req.Goal);
            return Results.Created($"/sessions/{session.Id}", session);
        });

        app.MapPut("/sessions/{id:guid}/end", async (
            Guid id, EndSessionRequest req, ISessionRepository sessions) =>
        {
            await sessions.EndSessionAsync(id, req.Summary);
            return Results.NoContent();
        });

        // ── /context ────────────────────────────────────────────────────────
        app.MapGet("/context/{project}", async (
            string project, int limit,
            ISessionRepository sessions) =>
        {
            limit = limit <= 0 ? 5 : limit;
            var ctx = await sessions.GetProjectContextAsync(project, limit);
            return Results.Ok(ctx);
        });

        // ── /stats ───────────────────────────────────────────────────────────
        app.MapGet("/stats", async (IMemoryRepository repo) =>
        {
            var stats = await repo.GetStatsAsync();
            return Results.Ok(stats);
        });

        AnsiConsole.MarkupLine($"[green]DevMemory API[/] listening on [bold]http://localhost:{settings.Port}[/]");
        AnsiConsole.MarkupLine("[dim]Press Ctrl+C to stop.[/]");

        await app.RunAsync(cancellationToken);
        return 0;
    }

    private sealed record SaveObservationRequest(
        string  Title,
        string  Type,
        string  Content,
        string? Project   = null,
        Guid?   SessionId = null,
        string[]? Tags    = null);

    private sealed record CreateSessionRequest(
        string? Project = null,
        string? Goal    = null);

    private sealed record EndSessionRequest(
        string? Summary = null);
}
