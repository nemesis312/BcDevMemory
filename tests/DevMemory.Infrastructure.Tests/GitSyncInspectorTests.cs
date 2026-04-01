using DevMemory.Infrastructure.Sync;

namespace DevMemory.Infrastructure.Tests;

public sealed class GitSyncInspectorTests
{
    [Fact]
    public void ParsePorcelainV2_Parses_Branch_And_Divergence()
    {
        var output = """
            # branch.oid 3f2d1a
            # branch.head main
            # branch.upstream origin/main
            # branch.ab +2 -1
            1 .M N... 100644 100644 100644 abcdef abcdef src/file.cs
            """;

        var info = GitSyncInspector.ParsePorcelainV2(output);

        Assert.True(info.IsRepository);
        Assert.Equal("main", info.Branch);
        Assert.True(info.HasUncommittedChanges);
        Assert.Equal(2, info.Ahead);
        Assert.Equal(1, info.Behind);
    }

    [Fact]
    public async Task InspectAsync_Returns_NotRepository_For_Plain_Directory()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"devmemory-git-check-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        try
        {
            var inspector = new GitSyncInspector();
            var info = await inspector.InspectAsync(dir);

            Assert.False(info.IsRepository);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}
