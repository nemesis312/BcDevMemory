using DevMemory.Infrastructure.Data;
using DevMemory.Infrastructure.Search;
using DevMemory.Infrastructure.Tests.Fixtures;

namespace DevMemory.Infrastructure.Tests;

[Collection(PostgresCollection.Name)]
public sealed class PostgresSearchServiceTests
{
    private readonly PostgresSessionRepository _sessions;
    private readonly PostgresMemoryRepository  _memory;
    private readonly PostgresSearchService     _search;

    public PostgresSearchServiceTests(PostgresContainerFixture fixture)
    {
        _sessions = new PostgresSessionRepository(fixture.Context);
        _memory   = new PostgresMemoryRepository(fixture.Context);
        _search   = new PostgresSearchService(fixture.Context);
    }

    private async Task<Guid> NewSessionId(string project = "SearchTest") =>
        (await _sessions.CreateSessionAsync(project)).Id;

    [Fact]
    public async Task Search_Finds_Observation_By_Title_Keyword()
    {
        var sid = await NewSessionId("SearchProj");
        await _memory.SaveObservationAsync(sid,
            title:   "Multi-tenant row-level security",
            type:    "security-pattern",
            content: "Implemented RLS using tenant_id column.",
            project: "SearchProj");

        // FTS index is synchronous (STORED column) — no delay needed.
        var results = (await _search.SearchAsync("multi-tenant", "SearchProj")).ToList();

        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Title.Contains("Multi-tenant", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Search_Returns_Results_Ordered_By_Rank()
    {
        var sid = await NewSessionId("RankProj");

        // "authentication" appears in both title and content → higher rank
        await _memory.SaveObservationAsync(sid,
            "JWT authentication pattern",
            "security-pattern",
            "What: Use JWT for authentication. Why: Stateless auth scales better.",
            project: "RankProj");

        // "authentication" only in content → lower rank
        await _memory.SaveObservationAsync(sid,
            "Database indexes",
            "performance-fix",
            "Add index for authentication token lookup.",
            project: "RankProj");

        var results = (await _search.SearchAsync("authentication", "RankProj")).ToList();

        Assert.True(results.Count >= 2);
        Assert.True(results[0].Rank >= results[1].Rank);
    }

    [Fact]
    public async Task Search_Respects_Project_Filter()
    {
        var sA = await NewSessionId("FilterA");
        var sB = await NewSessionId("FilterB");

        await _memory.SaveObservationAsync(sA, "Caching strategy", "pattern",
            "Redis caching implementation", project: "FilterA");
        await _memory.SaveObservationAsync(sB, "Caching approach",  "pattern",
            "Redis caching for FilterB",    project: "FilterB");

        var resultsA = (await _search.SearchAsync("caching", "FilterA")).ToList();

        Assert.All(resultsA, r => Assert.Equal("FilterA", r.Project));
    }

    [Fact]
    public async Task Search_Returns_Content_Preview_Not_Full_Content()
    {
        var sid = await NewSessionId("PreviewProj");
        var longContent = new string('x', 500); // > 200 chars

        await _memory.SaveObservationAsync(sid,
            "Long content observation", "discovery", longContent, "PreviewProj");

        var results = (await _search.SearchAsync("Long content", "PreviewProj")).ToList();

        Assert.NotEmpty(results);
        // Search results have ContentPreview (≤200 chars), not full content
        Assert.NotNull(results[0].ContentPreview);
        Assert.True(results[0].ContentPreview!.Length <= 200);
    }

    [Fact]
    public async Task GetTimeline_Returns_Anchor_Plus_Neighbors()
    {
        var sid = await NewSessionId("TimelineProj");

        var obs1 = await _memory.SaveObservationAsync(sid, "First",  "pattern", "...", "TimelineProj");
        var obs2 = await _memory.SaveObservationAsync(sid, "Second", "pattern", "...", "TimelineProj");
        var obs3 = await _memory.SaveObservationAsync(sid, "Third",  "pattern", "...", "TimelineProj");
        var obs4 = await _memory.SaveObservationAsync(sid, "Fourth", "pattern", "...", "TimelineProj");
        var obs5 = await _memory.SaveObservationAsync(sid, "Fifth",  "pattern", "...", "TimelineProj");

        // Timeline around obs3 (middle): before=[obs1,obs2], anchor=obs3, after=[obs4,obs5]
        var timeline = (await _search.GetTimelineAsync(obs3.Id, beforeCount: 2, afterCount: 2)).ToList();

        Assert.True(timeline.Count >= 3); // at minimum: 1 before + anchor + 1 after
        Assert.Contains(timeline, o => o.Id == obs3.Id); // anchor present
    }
}
