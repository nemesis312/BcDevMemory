using DevMemory.Core.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DevMemory.Api.Endpoints;

public static class SearchEndpoints
{
    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/search").WithTags("Search");

        group.MapGet("/", SearchAsync)
            .WithSummary("Full-text search across observations");

        group.MapGet("/timeline/{observationId:guid}", TimelineAsync)
            .WithSummary("Get observations around a specific observation in time");

        return app;
    }

    private static async Task<IResult> SearchAsync(
        string q, string? project, int limit = 10,
        string? type = null, string? tags = null,
        ISearchService search = null!)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Results.BadRequest("Query parameter 'q' is required.");

        var tagArray = tags?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var results = await search.SearchAsync(q, project, limit, type, tagArray);
        return Results.Ok(results);
    }

    private static async Task<IResult> TimelineAsync(
        Guid observationId, int before = 3, int after = 3,
        ISearchService search = null!)
    {
        var results = await search.GetTimelineAsync(observationId, before, after);
        return Results.Ok(results);
    }
}
