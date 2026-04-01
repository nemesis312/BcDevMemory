using DevMemory.Core.Models;
using DevMemory.Infrastructure.Data;
using DevMemory.Infrastructure.Sync;

namespace DevMemory.Infrastructure.Tests;

public sealed class SyncServiceTests
{
    [Fact]
    public async Task Export_Then_Import_Is_Idempotent_For_Sqlite()
    {
        var root = Path.Combine(Path.GetTempPath(), $"devmemory-sync-e2e-{Guid.NewGuid():N}");
        var syncPath = Path.Combine(root, "sync");
        var srcDb = Path.Combine(root, "source.db");
        var dstDb = Path.Combine(root, "target.db");

        Directory.CreateDirectory(root);

        try
        {
            var sourceCtx = new SqliteContext(srcDb);
            await sourceCtx.EnsureSchemaAsync();

            var sourceSessions = new SqliteSessionRepository(sourceCtx);
            var sourceMemory = new SqliteMemoryRepository(sourceCtx);

            var session = await sourceSessions.CreateSessionAsync("ProjectA", "Sync test");
            await sourceMemory.SaveObservationAsync(
                session.Id,
                "Sync me",
                "pattern",
                "Observation from source",
                "ProjectA",
                ["sync"]);
            await sourceMemory.SavePromptAsync(session.Id, "Remember this prompt");

            var sourceSync = new SyncService(
                new SyncRuntimeOptions { StorageProvider = "SQLite", ConfiguredSyncPath = syncPath },
                sourceCtx);

            var exportResult = await sourceSync.ExportAsync(new SyncOptions { AllProjects = true, SyncPath = syncPath });

            Assert.True(exportResult.Success);
            Assert.NotNull(exportResult.ChunkId);
            Assert.Equal(1, exportResult.ExportedObservations);

            var targetCtx = new SqliteContext(dstDb);
            await targetCtx.EnsureSchemaAsync();

            var targetSync = new SyncService(
                new SyncRuntimeOptions { StorageProvider = "SQLite", ConfiguredSyncPath = syncPath },
                targetCtx);

            var importResult = await targetSync.ImportAsync(new SyncOptions { AllProjects = true, SyncPath = syncPath });

            Assert.True(importResult.Success);
            Assert.Equal(1, importResult.ProcessedChunks);
            Assert.Equal(1, importResult.ImportedObservations);

            var targetMemory = new SqliteMemoryRepository(targetCtx);
            var imported = (await targetMemory.GetRecentObservationsAsync("ProjectA", 10)).ToList();
            Assert.Single(imported);
            Assert.Equal("Sync me", imported[0].Title);

            var importAgain = await targetSync.ImportAsync(new SyncOptions { AllProjects = true, SyncPath = syncPath });
            Assert.True(importAgain.Success);
            Assert.Equal(0, importAgain.ProcessedChunks);
            Assert.Equal(1, importAgain.SkippedChunks);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Import_Skips_Invalid_Chunk_When_Not_Strict()
    {
        var root = Path.Combine(Path.GetTempPath(), $"devmemory-sync-invalid-{Guid.NewGuid():N}");
        var syncPath = Path.Combine(root, "sync");
        var dbPath = Path.Combine(root, "target.db");
        Directory.CreateDirectory(Path.Combine(syncPath, "chunks"));

        try
        {
            await File.WriteAllTextAsync(Path.Combine(syncPath, "manifest.json"),
                """
                {
                  "version": 1,
                  "updated_at": "2026-01-01T00:00:00Z",
                  "chunks": [
                    {
                      "id": "badchunk0001",
                      "project": null,
                      "created_at": "2026-01-01T00:00:00Z",
                      "max_observation_created_at": "2026-01-01T00:00:00Z",
                      "item_count": 1
                    }
                  ]
                }
                """);
            await File.WriteAllTextAsync(Path.Combine(syncPath, "chunks", "badchunk0001.json.gz"), "not-gzip");

            var ctx = new SqliteContext(dbPath);
            await ctx.EnsureSchemaAsync();

            var sync = new SyncService(new SyncRuntimeOptions
            {
                StorageProvider = "SQLite",
                ConfiguredSyncPath = syncPath,
            }, ctx);

            var result = await sync.ImportAsync(new SyncOptions
            {
                AllProjects = true,
                SyncPath = syncPath,
                Strict = false,
            });

            Assert.True(result.Success);
            Assert.Equal(0, result.ProcessedChunks);
            Assert.Equal(1, result.InvalidChunks);
            Assert.Single(result.Warnings);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Import_Fails_Invalid_Chunk_When_Strict()
    {
        var root = Path.Combine(Path.GetTempPath(), $"devmemory-sync-strict-{Guid.NewGuid():N}");
        var syncPath = Path.Combine(root, "sync");
        var dbPath = Path.Combine(root, "target.db");
        Directory.CreateDirectory(Path.Combine(syncPath, "chunks"));

        try
        {
            await File.WriteAllTextAsync(Path.Combine(syncPath, "manifest.json"),
                """
                {
                  "version": 1,
                  "updated_at": "2026-01-01T00:00:00Z",
                  "chunks": [
                    {
                      "id": "badchunk0002",
                      "project": null,
                      "created_at": "2026-01-01T00:00:00Z",
                      "max_observation_created_at": "2026-01-01T00:00:00Z",
                      "item_count": 1
                    }
                  ]
                }
                """);
            await File.WriteAllTextAsync(Path.Combine(syncPath, "chunks", "badchunk0002.json.gz"), "not-gzip");

            var ctx = new SqliteContext(dbPath);
            await ctx.EnsureSchemaAsync();

            var sync = new SyncService(new SyncRuntimeOptions
            {
                StorageProvider = "SQLite",
                ConfiguredSyncPath = syncPath,
            }, ctx);

            var result = await sync.ImportAsync(new SyncOptions
            {
                AllProjects = true,
                SyncPath = syncPath,
                Strict = true,
            });

            Assert.False(result.Success);
            Assert.Contains("Failed to import chunk", result.Message);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
