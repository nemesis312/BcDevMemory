using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using DevMemory.Core.Interfaces;
using DevMemory.Core.Models;
using DevMemory.Core.Utilities;
using DevMemory.Infrastructure.Data;

namespace DevMemory.Infrastructure.Sync;

public sealed class SyncService : ISyncService
{
    private readonly SyncRuntimeOptions _runtimeOptions;
    private readonly SqliteContext? _sqlite;
    private readonly GitSyncInspector _git;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public SyncService(SyncRuntimeOptions runtimeOptions, SqliteContext? sqlite = null)
    {
        _runtimeOptions = runtimeOptions;
        _sqlite = sqlite;
        _git = new GitSyncInspector();
    }

    public async Task<SyncStatus> GetStatusAsync(SyncOptions options, CancellationToken cancellationToken = default)
    {
        var pathResolution = ResolvePath(options.SyncPath);
        var manifestPath = Path.Combine(pathResolution.Path, "manifest.json");
        var chunksPath = Path.Combine(pathResolution.Path, "chunks");
        var providerSupported = IsSqliteProvider();

        var status = new SyncStatus
        {
            StorageProvider = _runtimeOptions.StorageProvider,
            ProviderSupported = providerSupported,
            ResolvedSyncPath = pathResolution.Path,
            PathSource = pathResolution.Source.ToString(),
            ManifestPath = manifestPath,
            ChunksPath = chunksPath,
            SyncPathExists = Directory.Exists(pathResolution.Path),
            ManifestExists = File.Exists(manifestPath),
        };

        if (!providerSupported)
        {
            status.Messages.Add("Sync chunks are currently enabled only for SQLite storage.");
            status.Messages.Add("PostgreSQL/Neo4j are shared backends and do not need file-based chunk sync.");
            return status;
        }

        status.SyncPathWritable = CheckWritable(pathResolution.Path);

        if (!status.SyncPathExists)
        {
            status.Messages.Add("Sync path does not exist yet. Create it and initialize your memory sync repository.");
            return status;
        }

        if (!status.SyncPathWritable)
        {
            status.Messages.Add("Sync path exists but is not writable by the current user.");
            return status;
        }

        var transport = new FileSyncTransport(pathResolution.Path);
        var manifest = await transport.ReadManifestAsync(cancellationToken);
        status.ManifestVersion = manifest.Version;
        status.ChunkCount = manifest.Chunks.Count;

        var gitInfo = await _git.InspectAsync(pathResolution.Path, cancellationToken);
        status.GitRepositoryDetected = gitInfo.IsRepository;
        status.GitBranch = gitInfo.Branch;
        status.GitHasUncommittedChanges = gitInfo.HasUncommittedChanges;
        status.GitAheadCount = gitInfo.Ahead;
        status.GitBehindCount = gitInfo.Behind;

        if (!gitInfo.IsRepository)
            status.Messages.Add("Sync path is not a Git repository yet. Initialize it to enable push/pull workflow.");
        if (gitInfo.HasUncommittedChanges)
            status.Messages.Add("Sync repository has uncommitted changes.");
        if (gitInfo.Behind > 0)
            status.Messages.Add("Sync repository is behind upstream. Run git pull before importing chunks.");

        if (!Directory.Exists(chunksPath))
            status.Messages.Add("Chunks directory is missing. It will be created on the first export.");

        status.Messages.Add("Sync repository is ready.");
        return status;
    }

    public async Task<SyncExportResult> ExportAsync(SyncOptions options, CancellationToken cancellationToken = default)
    {
        if (!IsSqliteProvider() || _sqlite is null)
        {
            return new SyncExportResult
            {
                Success = false,
                Message = "Sync export is available only when SQLite storage is active.",
            };
        }

        if (options.AllProjects && !string.IsNullOrWhiteSpace(options.Project))
        {
            return new SyncExportResult
            {
                Success = false,
                Message = "Use either --all or --project, not both.",
            };
        }

        var pathResolution = ResolvePath(options.SyncPath);
        Directory.CreateDirectory(pathResolution.Path);

        var transport = new FileSyncTransport(pathResolution.Path);
        var manifest = await transport.ReadManifestAsync(cancellationToken);
        var since = GetLastExportedObservationDate(manifest, options);

        await using var connection = _sqlite!.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var observations = (await connection.QueryAsync<ObservationRow>(BuildObservationQuery(options), new
        {
            Project = options.Project,
            Since = since.ToString("O"),
        })).ToList();

        if (observations.Count == 0)
        {
            return new SyncExportResult
            {
                Success = true,
                Message = "No new observations to export.",
            };
        }

        var sessionIds = observations.Select(x => x.Session_Id).Distinct().ToArray();
        var sessions = (await connection.QueryAsync<SessionRow>(
            "SELECT * FROM sessions WHERE id IN @Ids", new { Ids = sessionIds })).ToList();

        var prompts = (await connection.QueryAsync<PromptRow>(
            "SELECT * FROM prompts WHERE session_id IN @Ids", new { Ids = sessionIds })).ToList();

        var chunk = new SyncChunkDocument
        {
            CreatedAt = DateTime.UtcNow,
            Project = options.AllProjects ? null : options.Project,
            Sessions = sessions.Select(ToRecord).ToList(),
            Observations = observations.Select(ToRecord).ToList(),
            Prompts = prompts.Select(ToRecord).ToList(),
        };

        var payload = JsonSerializer.SerializeToUtf8Bytes(chunk, JsonOpts);
        var chunkId = ComputeChunkId(payload);
        chunk.ChunkId = chunkId;
        payload = JsonSerializer.SerializeToUtf8Bytes(chunk, JsonOpts);

        await transport.WriteChunkAsync(chunkId, payload, cancellationToken);

        manifest.UpdatedAt = DateTime.UtcNow;
        manifest.Chunks.Add(new SyncChunkEntry
        {
            Id = chunkId,
            Project = chunk.Project,
            CreatedAt = DateTime.UtcNow,
            MaxObservationCreatedAt = chunk.Observations.Max(o => o.CreatedAt),
            ItemCount = chunk.Observations.Count + chunk.Sessions.Count + chunk.Prompts.Count,
        });

        await transport.WriteManifestAsync(manifest, cancellationToken);

        return new SyncExportResult
        {
            Success = true,
            Message = $"Exported chunk {chunkId}.",
            ChunkId = chunkId,
            ExportedSessions = chunk.Sessions.Count,
            ExportedObservations = chunk.Observations.Count,
            ExportedPrompts = chunk.Prompts.Count,
        };
    }

    public async Task<SyncImportResult> ImportAsync(SyncOptions options, CancellationToken cancellationToken = default)
    {
        if (!IsSqliteProvider() || _sqlite is null)
        {
            return new SyncImportResult
            {
                Success = false,
                Message = "Sync import is available only when SQLite storage is active.",
            };
        }

        if (options.AllProjects && !string.IsNullOrWhiteSpace(options.Project))
        {
            return new SyncImportResult
            {
                Success = false,
                Message = "Use either --all or --project, not both.",
            };
        }

        var pathResolution = ResolvePath(options.SyncPath);

        var gitInfo = await _git.InspectAsync(pathResolution.Path, cancellationToken);
        if (gitInfo.IsRepository && gitInfo.Behind > 0)
        {
            return new SyncImportResult
            {
                Success = false,
                Message = "Sync repo is behind upstream. Run git pull in sync path before --import.",
            };
        }

        var transport = new FileSyncTransport(pathResolution.Path);
        var manifest = await transport.ReadManifestAsync(cancellationToken);
        var entries = FilterEntries(manifest.Chunks, options).OrderBy(x => x.CreatedAt).ToList();

        if (entries.Count == 0)
        {
            return new SyncImportResult
            {
                Success = true,
                Message = "No chunks found to import.",
            };
        }

        var result = new SyncImportResult { Success = true };

        await using var connection = _sqlite!.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        foreach (var entry in entries)
        {
            if (await IsChunkImportedAsync(connection, entry.Id))
            {
                result.SkippedChunks++;
                continue;
            }

            var bytes = await transport.ReadChunkAsync(entry.Id, cancellationToken);
            var chunk = JsonSerializer.Deserialize<SyncChunkDocument>(bytes, JsonOpts)
                ?? throw new InvalidOperationException($"Chunk {entry.Id} has invalid payload.");

            await using var tx = await connection.BeginTransactionAsync(cancellationToken);

            foreach (var session in chunk.Sessions)
            {
                var inserted = await connection.ExecuteAsync(
                    """
                    INSERT OR IGNORE INTO sessions (id, project, goal, summary, files_changed, started_at, ended_at, is_active)
                    VALUES (@Id, @Project, @Goal, @Summary, @FilesChanged, @StartedAt, @EndedAt, @IsActive)
                    """,
                    new
                    {
                        Id = session.Id.ToString(),
                        session.Project,
                        session.Goal,
                        session.Summary,
                        FilesChanged = JsonSerializer.Serialize(session.FilesChanged),
                        StartedAt = session.StartedAt.ToString("O"),
                        EndedAt = session.EndedAt?.ToString("O"),
                        IsActive = session.IsActive ? 1 : 0,
                    },
                    tx);
                result.ImportedSessions += inserted;
            }

            foreach (var obs in chunk.Observations)
            {
                var inserted = await connection.ExecuteAsync(
                    """
                    INSERT OR IGNORE INTO observations (id, session_id, title, type, content, project, tags, created_at)
                    VALUES (@Id, @SessionId, @Title, @Type, @Content, @Project, @Tags, @CreatedAt)
                    """,
                    new
                    {
                        Id = obs.Id.ToString(),
                        SessionId = obs.SessionId.ToString(),
                        obs.Title,
                        obs.Type,
                        obs.Content,
                        obs.Project,
                        Tags = JsonSerializer.Serialize(obs.Tags),
                        CreatedAt = obs.CreatedAt.ToString("O"),
                    },
                    tx);
                result.ImportedObservations += inserted;
            }

            foreach (var prompt in chunk.Prompts)
            {
                var inserted = await connection.ExecuteAsync(
                    """
                    INSERT OR IGNORE INTO prompts (id, session_id, content, created_at)
                    VALUES (@Id, @SessionId, @Content, @CreatedAt)
                    """,
                    new
                    {
                        Id = prompt.Id.ToString(),
                        SessionId = prompt.SessionId.ToString(),
                        prompt.Content,
                        CreatedAt = prompt.CreatedAt.ToString("O"),
                    },
                    tx);
                result.ImportedPrompts += inserted;
            }

            await connection.ExecuteAsync(
                "INSERT OR IGNORE INTO sync_chunks (chunk_id, imported_at, item_count) VALUES (@ChunkId, @ImportedAt, @ItemCount)",
                new
                {
                    ChunkId = entry.Id,
                    ImportedAt = DateTime.UtcNow.ToString("O"),
                    ItemCount = chunk.Observations.Count + chunk.Sessions.Count + chunk.Prompts.Count,
                },
                tx);

            await tx.CommitAsync(cancellationToken);
            result.ProcessedChunks++;
        }

        result.Message = $"Processed {result.ProcessedChunks} chunk(s), skipped {result.SkippedChunks}.";
        return result;
    }

    private SyncPathResolution ResolvePath(string? cliPath)
    {
        return SyncPathResolver.Resolve(
            cliPath,
            Environment.GetEnvironmentVariable,
            key => key.Equals("DevMemory:Sync:Path", StringComparison.Ordinal)
                ? _runtimeOptions.ConfiguredSyncPath
                : null);
    }

    private bool IsSqliteProvider() => _runtimeOptions.StorageProvider.Equals("SQLite", StringComparison.OrdinalIgnoreCase);

    private static bool CheckWritable(string path)
    {
        if (!Directory.Exists(path))
            return false;

        var probe = Path.Combine(path, $".write-test-{Guid.NewGuid():N}.tmp");

        try
        {
            using var stream = File.Create(probe);
            stream.WriteByte(0x20);
            stream.Flush();
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildObservationQuery(SyncOptions options)
    {
        if (options.AllProjects || string.IsNullOrWhiteSpace(options.Project))
        {
            return """
                SELECT * FROM observations
                WHERE created_at > @Since
                ORDER BY created_at ASC
                """;
        }

        return """
            SELECT * FROM observations
            WHERE project = @Project
              AND created_at > @Since
            ORDER BY created_at ASC
            """;
    }

    private static DateTime GetLastExportedObservationDate(SyncManifest manifest, SyncOptions options)
    {
        var entries = FilterEntries(manifest.Chunks, options);
        return entries.Any() ? entries.Max(x => x.MaxObservationCreatedAt) : DateTime.MinValue;
    }

    private static IEnumerable<SyncChunkEntry> FilterEntries(IEnumerable<SyncChunkEntry> entries, SyncOptions options)
    {
        if (options.AllProjects || string.IsNullOrWhiteSpace(options.Project))
            return entries;

        return entries.Where(x => string.Equals(x.Project, options.Project, StringComparison.OrdinalIgnoreCase));
    }

    private static string ComputeChunkId(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash).ToLowerInvariant()[..12];
    }

    private static async Task<bool> IsChunkImportedAsync(System.Data.Common.DbConnection connection, string chunkId)
    {
        var exists = await connection.QueryFirstOrDefaultAsync<long?>(
            "SELECT 1 FROM sync_chunks WHERE chunk_id = @ChunkId LIMIT 1",
            new { ChunkId = chunkId });
        return exists.HasValue;
    }

    private static SyncSessionRecord ToRecord(SessionRow row) => new()
    {
        Id = Guid.Parse(row.Id),
        Project = row.Project,
        Goal = row.Goal,
        Summary = row.Summary,
        FilesChanged = string.IsNullOrWhiteSpace(row.Files_Changed)
            ? []
            : JsonSerializer.Deserialize<string[]>(row.Files_Changed) ?? [],
        StartedAt = ParseUtc(row.Started_At),
        EndedAt = string.IsNullOrWhiteSpace(row.Ended_At) ? null : ParseUtc(row.Ended_At),
        IsActive = row.Is_Active == 1,
    };

    private static SyncObservationRecord ToRecord(ObservationRow row) => new()
    {
        Id = Guid.Parse(row.Id),
        SessionId = Guid.Parse(row.Session_Id),
        Title = row.Title,
        Type = row.Type,
        Content = row.Content,
        Project = row.Project,
        Tags = string.IsNullOrWhiteSpace(row.Tags)
            ? []
            : JsonSerializer.Deserialize<string[]>(row.Tags) ?? [],
        CreatedAt = ParseUtc(row.Created_At),
    };

    private static SyncPromptRecord ToRecord(PromptRow row) => new()
    {
        Id = Guid.Parse(row.Id),
        SessionId = Guid.Parse(row.Session_Id),
        Content = row.Content,
        CreatedAt = ParseUtc(row.Created_At),
    };

    private static DateTime ParseUtc(string value) =>
        DateTime.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind);

    private sealed class ObservationRow
    {
        public string Id { get; set; } = string.Empty;
        public string Session_Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? Project { get; set; }
        public string? Tags { get; set; }
        public string Created_At { get; set; } = string.Empty;
    }

    private sealed class SessionRow
    {
        public string Id { get; set; } = string.Empty;
        public string? Project { get; set; }
        public string? Goal { get; set; }
        public string? Summary { get; set; }
        public string? Files_Changed { get; set; }
        public string Started_At { get; set; } = string.Empty;
        public string? Ended_At { get; set; }
        public int Is_Active { get; set; }
    }

    private sealed class PromptRow
    {
        public string Id { get; set; } = string.Empty;
        public string Session_Id { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Created_At { get; set; } = string.Empty;
    }
}
