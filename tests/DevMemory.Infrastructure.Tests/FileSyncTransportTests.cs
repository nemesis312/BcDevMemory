using DevMemory.Infrastructure.Sync;

namespace DevMemory.Infrastructure.Tests;

public sealed class FileSyncTransportTests
{
    [Fact]
    public async Task ReadManifest_Returns_Default_When_File_Does_Not_Exist()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"devmemory-sync-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        try
        {
            var transport = new FileSyncTransport(dir);
            var manifest = await transport.ReadManifestAsync();

            Assert.Equal(1, manifest.Version);
            Assert.Empty(manifest.Chunks);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}
