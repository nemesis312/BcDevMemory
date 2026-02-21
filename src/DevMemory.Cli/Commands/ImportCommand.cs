using System.ComponentModel;
using DevMemory.Infrastructure.Export;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DevMemory.Cli.Commands;

internal sealed class ImportCommand : AsyncCommand<ImportCommand.Settings>
{
    private readonly ExportService _exporter;

    public ImportCommand(ExportService exporter) => _exporter = exporter;

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<file>")]
        [Description("Path to a DevMemory JSON export file")]
        public string File { get; set; } = string.Empty;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (!System.IO.File.Exists(settings.File))
        {
            AnsiConsole.MarkupLine($"[red]File not found:[/] {Markup.Escape(settings.File)}");
            return 1;
        }

        var json  = await System.IO.File.ReadAllTextAsync(settings.File, cancellationToken);
        var count = await _exporter.ImportJsonAsync(json, cancellationToken);

        AnsiConsole.MarkupLine($"[green]✓[/] Imported [bold]{count}[/] observations from [dim]{Markup.Escape(settings.File)}[/]");
        return 0;
    }
}
