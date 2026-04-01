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
}
