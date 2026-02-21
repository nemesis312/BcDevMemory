using DevMemory.Core.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DevMemory.Api.Endpoints;

public static class MemoryEndpoints
{
    public static IEndpointRouteBuilder MapMemoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/observations").WithTags("Observations");

        group.MapGet("/", GetRecentAsync)
            .WithSummary("List recent observations");

        group.MapGet("/{id:guid}", GetByIdAsync)
            .WithSummary("Get a single observation by ID");

        group.MapPost("/", SaveAsync)
            .WithSummary("Save a new observation");

        return app;
    }

    private static async Task<IResult> GetRecentAsync(
        string? project, int limit = 20,
        IMemoryRepository repo = null!)
    {
        var items = await repo.GetRecentObservationsAsync(project, limit);
        return Results.Ok(items);
    }

    private static async Task<IResult> GetByIdAsync(Guid id, IMemoryRepository repo)
    {
        var obs = await repo.GetObservationAsync(id);
        return obs is null ? Results.NotFound() : Results.Ok(obs);
    }

    private static async Task<IResult> SaveAsync(
        SaveObservationRequest req,
        IMemoryRepository memory,
        ISessionRepository sessions)
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
    }

    private sealed record SaveObservationRequest(
        string Title,
        string Type,
        string Content,
        string? Project = null,
        Guid? SessionId = null,
        string[]? Tags = null);
}
