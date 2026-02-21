using Dapper;
using DevMemory.Core.Entities;
using DevMemory.Core.Interfaces;
using DevMemory.Core.Utilities;
using Npgsql;
using NpgsqlTypes;

namespace DevMemory.Infrastructure.Data;

public sealed class PostgresMemoryRepository : IMemoryRepository
{
    private readonly DapperContext _context;

    public PostgresMemoryRepository(DapperContext context)
    {
        _context = context;
    }

    public async Task<Observation> SaveObservationAsync(
        Guid sessionId,
        string title,
        string type,
        string content,
        string? project = null,
        string[]? tags = null,
        CancellationToken cancellationToken = default)
    {
        // Strip <private> blocks before any DB write.
        content = PrivacyHelper.StripPrivateTags(content);

        const string sql =
            "SELECT * FROM sp_save_observation(@p_session_id, @p_title, @p_type, @p_content, @p_project, @p_tags)";

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        // Arrays need an explicit NpgsqlParameter — we build DynamicParameters
        // and inject the array param manually.
        var parameters = new SaveObservationParams(sessionId, title, type, content, project, tags ?? []);

        var row = await connection.QueryFirstAsync<ObservationRow>(sql, parameters);
        return row.ToEntity();
    }

    public async Task<Observation?> GetObservationAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM observations WHERE id = @Id";

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var row = await connection.QueryFirstOrDefaultAsync<ObservationRow>(sql, new { Id = id });
        return row?.ToEntity();
    }

    public async Task<IEnumerable<Observation>> GetObservationsBySessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT * FROM observations
            WHERE session_id = @SessionId
            ORDER BY created_at DESC
            """;

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<ObservationRow>(sql, new { SessionId = sessionId });
        return rows.Select(r => r.ToEntity());
    }

    public async Task<IEnumerable<Observation>> GetRecentObservationsAsync(
        string? project = null,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT * FROM observations
            WHERE @Project::VARCHAR IS NULL OR project = @Project
            ORDER BY created_at DESC
            LIMIT @Limit
            """;

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<ObservationRow>(sql, new { Project = project, Limit = limit });
        return rows.Select(r => r.ToEntity());
    }

    public async Task SavePromptAsync(
        Guid sessionId,
        string content,
        CancellationToken cancellationToken = default)
    {
        const string sql = "INSERT INTO prompts (session_id, content) VALUES (@SessionId, @Content)";

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(sql, new { SessionId = sessionId, Content = content });
    }

    public async Task<MemoryStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM sp_get_stats()";

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var row = await connection.QueryFirstAsync<StatsRow>(sql);
        return row.ToStats();
    }

    // ── helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Carries all parameters for sp_save_observation and adds them directly to the
    /// NpgsqlCommand. The TEXT[] array needs an explicit NpgsqlDbType — this is the
    /// only reliable way to pass arrays through Dapper to Npgsql.
    /// </summary>
    private sealed class SaveObservationParams : SqlMapper.IDynamicParameters
    {
        private readonly Guid     _sessionId;
        private readonly string   _title;
        private readonly string   _type;
        private readonly string   _content;
        private readonly string?  _project;
        private readonly string[] _tags;

        public SaveObservationParams(
            Guid sessionId, string title, string type,
            string content, string? project, string[] tags)
        {
            _sessionId = sessionId;
            _title     = title;
            _type      = type;
            _content   = content;
            _project   = project;
            _tags      = tags;
        }

        public void AddParameters(System.Data.IDbCommand command, SqlMapper.Identity identity)
        {
            var cmd = (NpgsqlCommand)command;
            cmd.Parameters.AddWithValue("p_session_id", _sessionId);
            cmd.Parameters.AddWithValue("p_title",      _title);
            cmd.Parameters.AddWithValue("p_type",       _type);
            cmd.Parameters.AddWithValue("p_content",    _content);
            cmd.Parameters.AddWithValue("p_project",    (object?)_project ?? DBNull.Value);
            cmd.Parameters.Add(new NpgsqlParameter("p_tags", NpgsqlDbType.Array | NpgsqlDbType.Text)
            {
                Value = _tags
            });
        }
    }

    // ── internal DTO ───────────────────────────────────────────────────────
    // Use a class with setters (not a record) so Dapper uses property assignment
    // and our StringArrayTypeHandler fires correctly for the Tags column.

    private sealed class ObservationRow
    {
        public Guid      Id             { get; set; }
        public Guid      Session_Id     { get; set; }
        public string    Title          { get; set; } = string.Empty;
        public string    Type           { get; set; } = string.Empty;
        public string    Content        { get; set; } = string.Empty;
        public string?   Project        { get; set; }
        public string[]? Tags           { get; set; }
        public DateTime  Created_At     { get; set; }
        public float?    Rank           { get; set; }
        public string?   Content_Preview { get; set; }

        public Observation ToEntity() => new()
        {
            Id             = Id,
            SessionId      = Session_Id,
            Title          = Title,
            Type           = Type,
            Content        = Content,
            Project        = Project,
            Tags           = Tags ?? [],
            CreatedAt      = Created_At,
            Rank           = Rank,
            ContentPreview = Content_Preview,
        };
    }

    private sealed class StatsRow
    {
        public long      Total_Observations { get; set; }
        public long      Total_Sessions     { get; set; }
        public long      Active_Sessions    { get; set; }
        public long      Projects           { get; set; }
        public DateTime? Oldest_Observation { get; set; }
        public DateTime? Newest_Observation { get; set; }

        public MemoryStats ToStats() => new()
        {
            TotalObservations = Total_Observations,
            TotalSessions     = Total_Sessions,
            ActiveSessions    = Active_Sessions,
            Projects          = Projects,
            OldestObservation = Oldest_Observation,
            NewestObservation = Newest_Observation,
        };
    }
}
