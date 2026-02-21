using DevMemory.Infrastructure.Data;
using DevMemory.Infrastructure.Tests.Fixtures;

namespace DevMemory.Infrastructure.Tests;

[Collection(PostgresCollection.Name)]
public sealed class PostgresMemoryRepositoryTests
{
    private readonly PostgresSessionRepository _sessions;
    private readonly PostgresMemoryRepository  _memory;

    public PostgresMemoryRepositoryTests(PostgresContainerFixture fixture)
    {
        _sessions = new PostgresSessionRepository(fixture.Context);
        _memory   = new PostgresMemoryRepository(fixture.Context);
    }

    // ── helpers ────────────────────────────────────────────────────────────

    private async Task<Guid> NewSessionId(string project = "MemTest")
    {
        var s = await _sessions.CreateSessionAsync(project);
        return s.Id;
    }

    // ── tests ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveObservation_Returns_Persisted_Observation()
    {
        var sessionId = await NewSessionId();

        var obs = await _memory.SaveObservationAsync(
            sessionId,
            title:   "Fixed N+1 query",
            type:    "bugfix",
            content: "What: Added .Include(). Why: Slow page load.",
            project: "MemTest",
            tags:    ["performance", "ef"]);

        Assert.NotEqual(Guid.Empty, obs.Id);
        Assert.Equal(sessionId, obs.SessionId);
        Assert.Equal("Fixed N+1 query", obs.Title);
        Assert.Equal("bugfix", obs.Type);
        Assert.Contains("performance", obs.Tags);
    }

    [Fact]
    public async Task SaveObservation_Strips_Private_Tags()
    {
        var sessionId = await NewSessionId();

        var obs = await _memory.SaveObservationAsync(
            sessionId,
            title:   "Config note",
            type:    "config-change",
            content: "Public content. <private>SECRET_KEY=abc123</private> End.");

        Assert.Contains("[REDACTED]", obs.Content);
        Assert.DoesNotContain("SECRET_KEY", obs.Content);
    }

    [Fact]
    public async Task GetObservation_Returns_Full_Content()
    {
        var sessionId = await NewSessionId();
        var saved = await _memory.SaveObservationAsync(sessionId, "Get test", "pattern",
            "Full content here", "MemTest");

        var fetched = await _memory.GetObservationAsync(saved.Id);

        Assert.NotNull(fetched);
        Assert.Equal(saved.Id, fetched.Id);
        Assert.Equal("Full content here", fetched.Content);
    }

    [Fact]
    public async Task GetObservation_Returns_Null_For_Unknown_Id()
    {
        var result = await _memory.GetObservationAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task GetObservationsBySession_Returns_All_For_Session()
    {
        var sessionId = await NewSessionId("SessionObs");

        await _memory.SaveObservationAsync(sessionId, "Obs A", "pattern", "Content A");
        await _memory.SaveObservationAsync(sessionId, "Obs B", "bugfix",  "Content B");

        var results = (await _memory.GetObservationsBySessionAsync(sessionId)).ToList();

        Assert.Equal(2, results.Count);
        Assert.Contains(results, o => o.Title == "Obs A");
        Assert.Contains(results, o => o.Title == "Obs B");
    }

    [Fact]
    public async Task GetRecentObservations_Respects_Project_Filter()
    {
        var sA = await NewSessionId("ProjA");
        var sB = await NewSessionId("ProjB");

        await _memory.SaveObservationAsync(sA, "A obs", "pattern", "...", project: "ProjA");
        await _memory.SaveObservationAsync(sB, "B obs", "pattern", "...", project: "ProjB");

        var projA = (await _memory.GetRecentObservationsAsync("ProjA")).ToList();

        Assert.All(projA, o => Assert.Equal("ProjA", o.Project));
        Assert.DoesNotContain(projA, o => o.Project == "ProjB");
    }

    [Fact]
    public async Task GetRecentObservations_Respects_Limit()
    {
        var sessionId = await NewSessionId("LimitTest");

        for (var i = 0; i < 5; i++)
            await _memory.SaveObservationAsync(sessionId, $"Obs {i}", "pattern", "...", "LimitTest");

        var limited = (await _memory.GetRecentObservationsAsync("LimitTest", limit: 3)).ToList();

        Assert.Equal(3, limited.Count);
    }
}
