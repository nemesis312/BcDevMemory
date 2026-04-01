using System.ComponentModel;
using DevMemory.Core.Interfaces;
using DevMemory.Core.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DevMemory.Cli.Commands;

internal sealed class SyncCommand : AsyncCommand<SyncCommand.Settings>
{
    private readonly ISyncService _sync;

    public SyncCommand(ISyncService sync) => _sync = sync;

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--status")]
        [Description("Show sync repository readiness and configuration status")]
        public bool Status { get; set; }

        [CommandOption("--import")]
        [Description("Import chunks from sync repository into local SQLite")]
        public bool Import { get; set; }

        [CommandOption("-p|--project <project>")]
        [Description("Optional project scope for future sync phases")]
        public string? Project { get; set; }

        [CommandOption("--all")]
        [Description("Include all projects in future sync phases")]
        public bool All { get; set; }

        [CommandOption("--sync-path <path>")]
        [Description("Override sync repository path")]
        public string? SyncPath { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (settings.Status && settings.Import)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] use either [bold]--status[/] or [bold]--import[/], not both.");
            return 1;
        }

        var options = new SyncOptions
        {
            Project = settings.Project,
            AllProjects = settings.All,
            SyncPath = settings.SyncPath,
        };

        if (settings.Status)
            return await ShowStatusAsync(options, cancellationToken);

        if (settings.Import)
            return await ImportAsync(options, cancellationToken);

        return await ExportAsync(options, cancellationToken);
    }

    private async Task<int> ShowStatusAsync(SyncOptions options, CancellationToken cancellationToken)
    {
        var status = await _sync.GetStatusAsync(options, cancellationToken);

        AnsiConsole.MarkupLine("[bold]Sync status[/]");
        AnsiConsole.MarkupLine($"Storage provider: [cyan]{Markup.Escape(status.StorageProvider)}[/]");
        AnsiConsole.MarkupLine($"Path source: [cyan]{Markup.Escape(status.PathSource)}[/]");
        AnsiConsole.MarkupLine($"Sync path: [cyan]{Markup.Escape(status.ResolvedSyncPath)}[/]");
        AnsiConsole.MarkupLine($"Manifest path: [dim]{Markup.Escape(status.ManifestPath)}[/]");
        AnsiConsole.MarkupLine($"Chunks path: [dim]{Markup.Escape(status.ChunksPath)}[/]");
        AnsiConsole.MarkupLine($"Path exists: {(status.SyncPathExists ? "[green]yes[/]" : "[red]no[/]")}");
        AnsiConsole.MarkupLine($"Path writable: {(status.SyncPathWritable ? "[green]yes[/]" : "[yellow]no[/]")}");
        AnsiConsole.MarkupLine($"Manifest exists: {(status.ManifestExists ? "[green]yes[/]" : "[yellow]no[/]")}");
        AnsiConsole.MarkupLine($"Git repo: {(status.GitRepositoryDetected ? "[green]yes[/]" : "[yellow]no[/]")}");

        if (status.GitRepositoryDetected)
        {
            AnsiConsole.MarkupLine($"Git branch: [cyan]{Markup.Escape(status.GitBranch ?? "(detached)")}[/]");
            AnsiConsole.MarkupLine($"Git dirty: {(status.GitHasUncommittedChanges ? "[yellow]yes[/]" : "[green]no[/]")}");
            AnsiConsole.MarkupLine($"Git ahead/behind: [cyan]+{status.GitAheadCount}[/]/[cyan]-{status.GitBehindCount}[/]");
        }

        if (status.ManifestVersion.HasValue)
            AnsiConsole.MarkupLine($"Manifest version: [cyan]{status.ManifestVersion.Value}[/]");

        AnsiConsole.MarkupLine($"Chunk count: [cyan]{status.ChunkCount}[/]");

        if (status.Messages.Count > 0)
        {
            AnsiConsole.MarkupLine("\n[bold]Notes[/]");
            foreach (var message in status.Messages)
                AnsiConsole.MarkupLine($"- {Markup.Escape(message)}");
        }

        if (!status.ProviderSupported)
            return 2;

        return 0;
    }

    private async Task<int> ExportAsync(SyncOptions options, CancellationToken cancellationToken)
    {
        var result = await _sync.ExportAsync(options, cancellationToken);
        if (!result.Success)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(result.Message)}");
            return 2;
        }

        if (result.ExportedItems == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]No changes:[/] {Markup.Escape(result.Message)}");
            return 0;
        }

        AnsiConsole.MarkupLine($"[green]✓[/] {Markup.Escape(result.Message)}");
        AnsiConsole.MarkupLine($"Sessions: [cyan]{result.ExportedSessions}[/]  Observations: [cyan]{result.ExportedObservations}[/]  Prompts: [cyan]{result.ExportedPrompts}[/]");
        return 0;
    }

    private async Task<int> ImportAsync(SyncOptions options, CancellationToken cancellationToken)
    {
        var result = await _sync.ImportAsync(options, cancellationToken);
        if (!result.Success)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(result.Message)}");
            return 2;
        }

        AnsiConsole.MarkupLine($"[green]✓[/] {Markup.Escape(result.Message)}");
        AnsiConsole.MarkupLine($"Imported - Sessions: [cyan]{result.ImportedSessions}[/], Observations: [cyan]{result.ImportedObservations}[/], Prompts: [cyan]{result.ImportedPrompts}[/]");
        return 0;
    }
}
