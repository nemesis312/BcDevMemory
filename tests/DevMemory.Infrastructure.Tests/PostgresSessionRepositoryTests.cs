using DevMemory.Infrastructure.Data;
using DevMemory.Infrastructure.Tests.Fixtures;

namespace DevMemory.Infrastructure.Tests;

[Collection(PostgresCollection.Name)]
public sealed class PostgresSessionRepositoryTests
{
    private readonly PostgresSessionRepository _repo;
    private readonly DapperContext _context;

    public PostgresSessionRepositoryTests(PostgresContainerFixture fixture)
    {
        _context = fixture.Context;
        _repo = new PostgresSessionRepository(_context);
    }

    [Fact]
    public async Task CreateSession_Returns_Session_With_Generated_Id()
    {
        var session = await _repo.CreateSessionAsync("TestProject", "My goal");

        Assert.NotEqual(Guid.Empty, session.Id);
        Assert.Equal("TestProject", session.Project);
        Assert.Equal("My goal", session.Goal);
        Assert.True(session.IsActive);
        Assert.Null(session.EndedAt);
    }

    [Fact]
    public async Task GetActiveSession_Returns_Most_Recent_Active_Session()
    {
        var created = await _repo.CreateSessionAsync("ActiveProj");

        var active = await _repo.GetActiveSessionAsync("ActiveProj");

        Assert.NotNull(active);
        Assert.Equal(created.Id, active.Id);
    }

    [Fact]
    public async Task GetActiveSession_Returns_Null_When_No_Active_Session()
    {
        const string project = "InactiveProj";
        var session = await _repo.CreateSessionAsync(project);
        await _repo.EndSessionAsync(session.Id, "Done");

        var active = await _repo.GetActiveSessionAsync(project);

        Assert.Null(active);
    }

    [Fact]
    public async Task EndSession_Sets_IsActive_False_And_EndedAt()
    {
        var session = await _repo.CreateSessionAsync("EndTest");

        await _repo.EndSessionAsync(session.Id, "Session summary here");

        var ended = await _repo.GetSessionAsync(session.Id);
        Assert.NotNull(ended);
        Assert.False(ended.IsActive);
        Assert.NotNull(ended.EndedAt);
        Assert.Equal("Session summary here", ended.Summary);
    }

    [Fact]
    public async Task UpdateSession_Patches_Summary_And_FilesChanged()
    {
        var session = await _repo.CreateSessionAsync("UpdateTest");

        await _repo.UpdateSessionAsync(
            session.Id,
            summary: "Partial update",
            filesChanged: ["src/Foo.cs", "src/Bar.cs"]);

        var updated = await _repo.GetSessionAsync(session.Id);
        Assert.NotNull(updated);
        Assert.Equal("Partial update", updated.Summary);
        Assert.Equal(["src/Foo.cs", "src/Bar.cs"], updated.FilesChanged);
    }

    [Fact]
    public async Task GetRecentSessions_Returns_Sessions_Ordered_By_Started_At()
    {
        const string project = "RecentTest";
        var s1 = await _repo.CreateSessionAsync(project);
        var s2 = await _repo.CreateSessionAsync(project);

        var recent = (await _repo.GetRecentSessionsAsync(project, limit: 5)).ToList();

        Assert.True(recent.Count >= 2);
        // Most recent first
        Assert.Equal(s2.Id, recent[0].Id);
        Assert.Equal(s1.Id, recent[1].Id);
    }

    [Fact]
    public async Task GetSession_Returns_Null_For_Unknown_Id()
    {
        var result = await _repo.GetSessionAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task CloseStaleSessionsAsync_Closes_Sessions_Older_Than_Threshold()
    {
        // Create a session
        var session = await _repo.CreateSessionAsync("StaleProject");

        // Manually age the session to 25 hours ago via direct SQL
        await using var conn = _context.CreateConnection();
        await conn.OpenAsync();
        await Dapper.SqlMapper.ExecuteAsync(conn,
            "UPDATE sessions SET started_at = NOW() - INTERVAL '25 hours' WHERE id = @Id",
            new { Id = session.Id });

        // Close sessions older than 24 hours
        var closedCount = await _repo.CloseStaleSessionsAsync(TimeSpan.FromHours(24));

        Assert.True(closedCount >= 1);

        var closed = await _repo.GetSessionAsync(session.Id);
        Assert.NotNull(closed);
        Assert.False(closed.IsActive);
        Assert.Contains("[Auto-closed: stale session]", closed.Summary ?? "");
    }

    [Fact]
    public async Task CloseStaleSessionsAsync_Does_Not_Close_Recent_Sessions()
    {
        var session = await _repo.CreateSessionAsync("RecentProject");

        // Close sessions older than 24 hours (should not close our new session)
        var closedCount = await _repo.CloseStaleSessionsAsync(TimeSpan.FromHours(24));

        // The new session should still be active
        var stillActive = await _repo.GetSessionAsync(session.Id);
        Assert.NotNull(stillActive);
        Assert.True(stillActive.IsActive);
    }

    [Fact]
    public async Task GetStaleActiveSessionAsync_Returns_Stale_Session()
    {
        var session = await _repo.CreateSessionAsync("StaleDetectProj");

        // Age the session
        await using var conn = _context.CreateConnection();
        await conn.OpenAsync();
        await Dapper.SqlMapper.ExecuteAsync(conn,
            "UPDATE sessions SET started_at = NOW() - INTERVAL '12 hours' WHERE id = @Id",
            new { Id = session.Id });

        // Look for sessions older than 8 hours
        var stale = await _repo.GetStaleActiveSessionAsync("StaleDetectProj", TimeSpan.FromHours(8));

        Assert.NotNull(stale);
        Assert.Equal(session.Id, stale.Id);
    }

    [Fact]
    public async Task GetStaleActiveSessionAsync_Returns_Null_For_Recent_Session()
    {
        var session = await _repo.CreateSessionAsync("FreshProj");

        // Look for sessions older than 8 hours (new session should not match)
        var stale = await _repo.GetStaleActiveSessionAsync("FreshProj", TimeSpan.FromHours(8));

        Assert.Null(stale);
    }
}
