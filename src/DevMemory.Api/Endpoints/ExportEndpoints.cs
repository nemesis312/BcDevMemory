using DevMemory.Infrastructure.Export;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DevMemory.Api.Endpoints;

public static class ExportEndpoints
{
    public static IEndpointRouteBuilder MapExportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/export").WithTags("Export");

        group.MapGet("/json", ExportJsonAsync)
            .WithSummary("Export observations as JSON");

        group.MapGet("/markdown", ExportMarkdownAsync)
            .WithSummary("Export observations as Markdown");

        group.MapPost("/import", ImportAsync)
            .WithSummary("Import observations from a JSON export");

        return app;
    }

    private static async Task<IResult> ExportJsonAsync(
        string? project, int limit = 1000,
        ExportService exporter = null!)
    {
        var json = await exporter.ExportJsonAsync(project, limit);
        return Results.Content(json, "application/json");
    }

    private static async Task<IResult> ExportMarkdownAsync(
        string? project, int limit = 1000,
        ExportService exporter = null!)
    {
        var md = await exporter.ExportMarkdownAsync(project, limit);
        return Results.Content(md, "text/markdown");
    }

    private static async Task<IResult> ImportAsync(
        ImportRequest req, ExportService exporter)
    {
        var count = await exporter.ImportJsonAsync(req.Json);
        return Results.Ok(new { imported = count });
    }

    private sealed record ImportRequest(string Json);
}
