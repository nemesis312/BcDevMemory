using DevMemory.Core.Interfaces;
using DevMemory.Core.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DevMemory.Api.Endpoints;

public static class SyncEndpoints
{
    public static IEndpointRouteBuilder MapSyncEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/sync").WithTags("Sync");

        group.MapPost("/init", InitAsync)
            .WithSummary("Initialize sync repository structure");

        group.MapGet("/status", StatusAsync)
            .WithSummary("Get sync repository status");

        group.MapPost("/export", ExportAsync)
            .WithSummary("Export SQLite memory delta to sync chunk");

        group.MapPost("/import", ImportAsync)
            .WithSummary("Import chunks from sync repository into SQLite");

        return app;
    }

    private static async Task<IResult> InitAsync(SyncRequest request, ISyncService sync)
    {
        var result = await sync.InitializeAsync(ToOptions(request));
        return result.Success ? Results.Ok(result) : Results.BadRequest(result);
    }

    private static async Task<IResult> StatusAsync(string? syncPath, string? project, bool all, ISyncService sync)
    {
        var status = await sync.GetStatusAsync(new SyncOptions
        {
            SyncPath = syncPath,
            Project = project,
            AllProjects = all,
        });
        return Results.Ok(status);
    }

    private static async Task<IResult> ExportAsync(SyncRequest request, ISyncService sync)
    {
        var result = await sync.ExportAsync(ToOptions(request));
        return result.Success ? Results.Ok(result) : Results.BadRequest(result);
    }

    private static async Task<IResult> ImportAsync(SyncRequest request, ISyncService sync)
    {
        var result = await sync.ImportAsync(ToOptions(request));
        return result.Success ? Results.Ok(result) : Results.BadRequest(result);
    }

    private static SyncOptions ToOptions(SyncRequest request) => new()
    {
        SyncPath = request.SyncPath,
        Project = request.Project,
        AllProjects = request.All,
        Strict = request.Strict,
    };

    private sealed record SyncRequest(
        string? SyncPath = null,
        string? Project = null,
        bool All = false,
        bool Strict = false);
}
