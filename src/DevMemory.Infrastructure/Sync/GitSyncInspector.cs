using System.Diagnostics;

namespace DevMemory.Infrastructure.Sync;

public sealed class GitSyncInspector
{
    public async Task<GitSyncInfo> InspectAsync(string path, CancellationToken cancellationToken = default)
    {
        var isRepo = await IsGitRepositoryAsync(path, cancellationToken);
        if (!isRepo)
            return new GitSyncInfo { IsRepository = false };

        var statusOutput = await RunGitAsync(path, "status --porcelain=2 --branch", cancellationToken);
        return ParsePorcelainV2(statusOutput);
    }

    public async Task<bool> InitializeRepositoryAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            await RunGitAsync(path, "init", cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> IsGitRepositoryAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var output = await RunGitAsync(path, "rev-parse --is-inside-work-tree", cancellationToken);
            return output.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<string> RunGitAsync(string path, string args, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"-C \"{path}\" {args}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"git {args} failed: {stderr}");

        return stdout;
    }

    public static GitSyncInfo ParsePorcelainV2(string output)
    {
        var info = new GitSyncInfo { IsRepository = true };
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            if (line.StartsWith("# branch.head ", StringComparison.Ordinal))
            {
                info.Branch = line[14..].Trim();
                continue;
            }

            if (line.StartsWith("# branch.ab ", StringComparison.Ordinal))
            {
                var parts = line[12..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    if (part.StartsWith('+') && int.TryParse(part[1..], out var ahead))
                        info.Ahead = ahead;
                    if (part.StartsWith('-') && int.TryParse(part[1..], out var behind))
                        info.Behind = behind;
                }
                continue;
            }

            if (!line.StartsWith('#'))
                info.HasUncommittedChanges = true;
        }

        return info;
    }
}

public sealed class GitSyncInfo
{
    public bool IsRepository { get; set; }
    public string? Branch { get; set; }
    public bool HasUncommittedChanges { get; set; }
    public int Ahead { get; set; }
    public int Behind { get; set; }
}
