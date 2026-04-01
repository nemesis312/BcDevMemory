using DevMemory.Core.Utilities;

namespace DevMemory.Core.Tests;

public sealed class SyncPathResolverTests
{
    [Fact]
    public void Resolve_Uses_Cli_Path_First()
    {
        var result = SyncPathResolver.Resolve(
            "~/from-cli",
            _ => "/env/path",
            _ => "/config/path");

        Assert.Equal(SyncPathSource.Cli, result.Source);
        Assert.Contains("from-cli", result.Path);
    }

    [Fact]
    public void Resolve_Uses_Environment_When_Cli_Missing()
    {
        var result = SyncPathResolver.Resolve(
            null,
            key => key == "DEVMEMORY_SYNC_PATH" ? "/env/path" : null,
            _ => "/config/path");

        Assert.Equal(SyncPathSource.Environment, result.Source);
        Assert.Equal(Path.GetFullPath("/env/path"), result.Path);
    }

    [Fact]
    public void Resolve_Uses_Config_When_Cli_And_Env_Missing()
    {
        var result = SyncPathResolver.Resolve(
            null,
            _ => null,
            key => key == "DevMemory:Sync:Path" ? "/config/path" : null);

        Assert.Equal(SyncPathSource.Configuration, result.Source);
        Assert.Equal(Path.GetFullPath("/config/path"), result.Path);
    }

    [Fact]
    public void Resolve_Uses_Default_When_No_Source_Is_Set()
    {
        var result = SyncPathResolver.Resolve(
            null,
            _ => null,
            _ => null);

        Assert.Equal(SyncPathSource.Default, result.Source);
        Assert.Contains(".devmemory-sync", result.Path);
    }
}
