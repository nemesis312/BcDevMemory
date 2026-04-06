using System.Text.Json;
using Dapper;
using DevMemory.Core.Entities;
using DevMemory.Core.Interfaces;

namespace DevMemory.Infrastructure.Data;

public sealed class SqliteArtifactRepository : IArtifactRepository
{
    private readonly SqliteContext _context;

    public SqliteArtifactRepository(SqliteContext context) => _context = context;

    public async Task<Artifact> SaveArtifactAsync(
        string runId,
        string projectName,
        string type,
        string content,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        // Determine next version for this run+type
        const string versionSql = """
            SELECT COALESCE(MAX(version), 0) + 1
            FROM artifacts
            WHERE run_id = @RunId AND type = @Type
            """;
        var version = await connection.ExecuteScalarAsync<int>(versionSql,
            new { RunId = runId, Type = type });

        var id        = Guid.NewGuid().ToString();
        var createdAt = DateTime.UtcNow;
        var metaJson  = metadata is null ? null : JsonSerializer.Serialize(metadata);

        const string insertSql = """
            INSERT INTO artifacts (id, run_id, project_name, type, content, version, metadata, created_at)
            VALUES (@Id, @RunId, @ProjectName, @Type, @Content, @Version, @Metadata, @CreatedAt)
            """;

        await connection.ExecuteAsync(insertSql, new
        {
            Id          = id,
            RunId       = runId,
            ProjectName = projectName,
            Type        = type,
            Content     = content,
            Version     = version,
            Metadata    = metaJson,
            CreatedAt   = createdAt.ToString("O"),
        });

        return new Artifact
        {
            Id          = id,
            RunId       = runId,
            ProjectName = projectName,
            Type        = type,
            Content     = content,
            Version     = version,
            Metadata    = metadata,
            CreatedAt   = createdAt,
        };
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

        var row = await connection.QueryFirstOrDefaultAsync<ArtifactRow>(sql,
            new { RunId = runId, Type = type });
        return row?.ToEntity();
    }

    public async Task<IEnumerable<Artifact>> ListRunArtifactsAsync(
        string runId,
        CancellationToken cancellationToken = default)
    {
        // Return only the latest version of each type for the run
        const string sql = """
            SELECT a.* FROM artifacts a
            INNER JOIN (
                SELECT run_id, type, MAX(version) AS max_version
                FROM artifacts
                WHERE run_id = @RunId
                GROUP BY run_id, type
            ) latest ON a.run_id = latest.run_id
                     AND a.type = latest.type
                     AND a.version = latest.max_version
            ORDER BY a.created_at ASC
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
        // Return the latest version of each type per run, ordered by creation date desc
        const string sql = """
            SELECT a.* FROM artifacts a
            INNER JOIN (
                SELECT run_id, type, MAX(version) AS max_version
                FROM artifacts
                WHERE project_name = @ProjectName
                GROUP BY run_id, type
            ) latest ON a.run_id = latest.run_id
                     AND a.type = latest.type
                     AND a.version = latest.max_version
            ORDER BY a.created_at DESC
            LIMIT 50
            """;

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<ArtifactRow>(sql, new { ProjectName = projectName });
        return rows.Select(r => r.ToEntity());
    }

    // ── internal DTOs ────────────────────────────────────────────────────────

    private sealed class ArtifactRow
    {
        public string  Id           { get; set; } = string.Empty;
        public string  Run_Id       { get; set; } = string.Empty;
        public string  Project_Name { get; set; } = string.Empty;
        public string  Type         { get; set; } = string.Empty;
        public string  Content      { get; set; } = string.Empty;
        public int     Version      { get; set; }
        public string? Metadata     { get; set; }
        public string  Created_At   { get; set; } = string.Empty;

        public Artifact ToEntity() => new()
        {
            Id          = Id,
            RunId       = Run_Id,
            ProjectName = Project_Name,
            Type        = Type,
            Content     = Content,
            Version     = Version,
            Metadata    = Metadata is null
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, string>>(Metadata),
            CreatedAt   = DateTime.Parse(Created_At, null,
                System.Globalization.DateTimeStyles.RoundtripKind),
        };
    }
}
