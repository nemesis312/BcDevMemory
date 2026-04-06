using System.Text.Json;
using Dapper;
using DevMemory.Core.Entities;
using DevMemory.Core.Interfaces;
using Npgsql;
using NpgsqlTypes;

namespace DevMemory.Infrastructure.Data;

public sealed class PostgresArtifactRepository : IArtifactRepository
{
    private readonly DapperContext _context;

    public PostgresArtifactRepository(DapperContext context) => _context = context;

    public async Task<Artifact> SaveArtifactAsync(
        string runId,
        string projectName,
        string type,
        string content,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        const string sql =
            "SELECT * FROM sp_save_artifact(@p_run_id, @p_project_name, @p_type, @p_content, @p_metadata)";

        var metadataJson = metadata is null
            ? null
            : JsonSerializer.Serialize(metadata);

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var parameters = new SaveArtifactParams(runId, projectName, type, content, metadataJson);
        var row = await connection.QueryFirstAsync<ArtifactRow>(sql, parameters);
        return row.ToEntity();
    }

    public async Task<Artifact?> GetLatestArtifactAsync(
        string runId,
        string type,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT * FROM artifacts
            WHERE run_id = @RunId AND type = @Type
            ORDER BY version DESC
            LIMIT 1
            """;

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var row = await connection.QueryFirstOrDefaultAsync<ArtifactRow>(sql, new { RunId = runId, Type = type });
        return row?.ToEntity();
    }

    public async Task<IEnumerable<Artifact>> ListRunArtifactsAsync(
        string runId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT DISTINCT ON (type) *
            FROM artifacts
            WHERE run_id = @RunId
            ORDER BY type, version DESC
            """;

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<ArtifactRow>(sql, new { RunId = runId });
        return rows.Select(r => r.ToEntity());
    }

    public async Task<IEnumerable<Artifact>> ListProjectArtifactsAsync(
        string projectName,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT DISTINCT ON (run_id, type) *
            FROM artifacts
            WHERE project_name = @ProjectName
            ORDER BY run_id, type, version DESC, created_at DESC
            """;

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<ArtifactRow>(sql, new { ProjectName = projectName });
        return rows.Select(r => r.ToEntity());
    }

    // ── IDynamicParameters for JSONB ──────────────────────────────────────

    private sealed class SaveArtifactParams : SqlMapper.IDynamicParameters
    {
        private readonly string  _runId;
        private readonly string  _projectName;
        private readonly string  _type;
        private readonly string  _content;
        private readonly string? _metadataJson;

        public SaveArtifactParams(
            string runId, string projectName, string type,
            string content, string? metadataJson)
        {
            _runId        = runId;
            _projectName  = projectName;
            _type         = type;
            _content      = content;
            _metadataJson = metadataJson;
        }

        public void AddParameters(System.Data.IDbCommand command, SqlMapper.Identity identity)
        {
            var cmd = (NpgsqlCommand)command;
            cmd.Parameters.AddWithValue("p_run_id",       _runId);
            cmd.Parameters.AddWithValue("p_project_name", _projectName);
            cmd.Parameters.AddWithValue("p_type",         _type);
            cmd.Parameters.AddWithValue("p_content",      _content);
            cmd.Parameters.Add(new NpgsqlParameter("p_metadata", NpgsqlDbType.Jsonb)
            {
                Value = (object?)_metadataJson ?? DBNull.Value
            });
        }
    }

    // ── internal DTO ─────────────────────────────────────────────────────

    private sealed class ArtifactRow
    {
        public Guid     Id           { get; set; }
        public string   Run_Id       { get; set; } = string.Empty;
        public string   Project_Name { get; set; } = string.Empty;
        public string   Type         { get; set; } = string.Empty;
        public string   Content      { get; set; } = string.Empty;
        public int      Version      { get; set; }
        public string?  Metadata     { get; set; }
        public DateTime Created_At   { get; set; }

        public Artifact ToEntity() => new()
        {
            Id          = Id.ToString(),
            RunId       = Run_Id,
            ProjectName = Project_Name,
            Type        = Type,
            Content     = Content,
            Version     = Version,
            Metadata    = Metadata is null
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, string>>(Metadata),
            CreatedAt   = Created_At,
        };
    }
}
