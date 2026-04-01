namespace DevMemory.Core.Utilities;

public static class SyncPathResolver
{
    public static SyncPathResolution Resolve(
        string? cliPath,
        Func<string, string?> env,
        Func<string, string?> config)
    {
        var raw = !string.IsNullOrWhiteSpace(cliPath)
            ? cliPath
            : env("DEVMEMORY_SYNC_PATH")
                ?? config("DevMemory:Sync:Path")
                ?? "~/.devmemory-sync";

        var source = !string.IsNullOrWhiteSpace(cliPath)
            ? SyncPathSource.Cli
            : !string.IsNullOrWhiteSpace(env("DEVMEMORY_SYNC_PATH"))
                ? SyncPathSource.Environment
                : !string.IsNullOrWhiteSpace(config("DevMemory:Sync:Path"))
                    ? SyncPathSource.Configuration
                    : SyncPathSource.Default;

        return new SyncPathResolution(ExpandToAbsolutePath(raw), source);
    }

    private static string ExpandToAbsolutePath(string path)
    {
        var expanded = path.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        return Path.GetFullPath(expanded);
    }
}

public readonly record struct SyncPathResolution(string Path, SyncPathSource Source);

public enum SyncPathSource
{
    Cli,
    Environment,
    Configuration,
    Default,
}
