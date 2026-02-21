using DevMemory.Core.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DevMemory.Api.Endpoints;

public static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/sessions").WithTags("Sessions");

        group.MapGet("/", GetRecentAsync)
            .WithSummary("List recent sessions");

        group.MapGet("/active", GetActiveAsync)
            .WithSummary("Get the current active session for a project");

        group.MapGet("/{id:guid}", GetByIdAsync)
            .WithSummary("Get a session by ID");

        group.MapPost("/", CreateAsync)
            .WithSummary("Start a new session");

        group.MapPut("/{id:guid}/end", EndAsync)
            .WithSummary("End a session");

        // Context endpoint: recent completed sessions with observation summaries
        app.MapGet("/context/{project}", GetContextAsync)
            .WithTags("Sessions")
            .WithSummary("Get project context — recent sessions with observation summaries");

        // Stats
        app.MapGet("/stats", GetStatsAsync)
            .WithTags("Stats")
            .WithSummary("Get memory statistics");

        return app;
    }

    private static async Task<IResult> GetRecentAsync(
        string? project, int limit = 5,
        ISessionRepository sessions = null!)
    {
        var items = await sessions.GetRecentSessionsAsync(project, limit);
        return Results.Ok(items);
    }

    private static async Task<IResult> GetActiveAsync(
        string? project, ISessionRepository sessions)
    {
        var session = await sessions.GetActiveSessionAsync(project);
        return session is null ? Results.NotFound() : Results.Ok(session);
    }

    private static async Task<IResult> GetByIdAsync(Guid id, ISessionRepository sessions)
    {
        var session = await sessions.GetSessionAsync(id);
        return session is null ? Results.NotFound() : Results.Ok(session);
    }

    private static async Task<IResult> CreateAsync(
        CreateSessionRequest req, ISessionRepository sessions)
    {
        var session = await sessions.CreateSessionAsync(req.Project, req.Goal);
        return Results.Created($"/sessions/{session.Id}", session);
    }

    private static async Task<IResult> EndAsync(
        Guid id, EndSessionRequest req, ISessionRepository sessions)
    {
        await sessions.EndSessionAsync(id, req.Summary);
        return Results.NoContent();
    }

    private static async Task<IResult> GetContextAsync(
        string project, int limit = 5,
        ISessionRepository sessions = null!)
    {
        var ctx = await sessions.GetProjectContextAsync(project, limit);
        return Results.Ok(ctx);
    }

    private static async Task<IResult> GetStatsAsync(IMemoryRepository repo)
    {
        var stats = await repo.GetStatsAsync();
        return Results.Ok(stats);
    }

    private sealed record CreateSessionRequest(
        string? Project = null,
        string? Goal = null);

    private sealed record EndSessionRequest(
        string? Summary = null);
}
