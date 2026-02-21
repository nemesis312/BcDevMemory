using System.ComponentModel;
using DevMemory.Infrastructure.Export;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DevMemory.Cli.Commands;

internal sealed class ExportCommand : AsyncCommand<ExportCommand.Settings>
{
    private readonly ExportService _exporter;

    public ExportCommand(ExportService exporter) => _exporter = exporter;

    public sealed class Settings : CommandSettings
    {
        [CommandOption("-p|--project <project>")]
        [Description("Filter export to a specific project")]
        public string? Project { get; set; }

        [CommandOption("-f|--format <format>")]
        [Description("Export format: json (default) or markdown")]
        [DefaultValue("json")]
        public string Format { get; set; } = "json";

        [CommandOption("-o|--output <file>")]
        [Description("Output file path (defaults to stdout)")]
        public string? Output { get; set; }

        [CommandOption("--limit <limit>")]
        [Description("Maximum observations to export")]
        [DefaultValue(1000)]
        public int Limit { get; set; } = 1000;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        string content;

        if (settings.Format.Equals("markdown", StringComparison.OrdinalIgnoreCase))
            content = await _exporter.ExportMarkdownAsync(settings.Project, settings.Limit, cancellationToken);
        else
            content = await _exporter.ExportJsonAsync(settings.Project, settings.Limit, cancellationToken);

        if (settings.Output is not null)
        {
            await File.WriteAllTextAsync(settings.Output, content, cancellationToken);
            AnsiConsole.MarkupLine($"[green]✓[/] Exported to [bold]{Markup.Escape(settings.Output)}[/]");
        }
        else
        {
            Console.WriteLine(content);
        }

        return 0;
    }
}
