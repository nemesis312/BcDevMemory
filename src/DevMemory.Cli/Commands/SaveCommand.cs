using System.ComponentModel;
using DevMemory.Core.Enums;
using DevMemory.Core.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DevMemory.Cli.Commands;

internal sealed class SaveCommand : AsyncCommand<SaveCommand.Settings>
{
    private readonly IMemoryRepository _memory;
    private readonly ISessionRepository _sessions;

    public SaveCommand(IMemoryRepository memory, ISessionRepository sessions)
    {
        _memory = memory;
        _sessions = sessions;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<title>")]
        [Description("Short title for the observation")]
        public string Title { get; set; } = string.Empty;

        [CommandArgument(1, "<content>")]
        [Description("Observation content — What/Why/Where format recommended")]
        public string Content { get; set; } = string.Empty;

        [CommandOption("-t|--type <type>")]
        [Description("Type: bugfix, architecture-decision, pattern, discovery, etc.")]
        [DefaultValue("pattern")]
        public string Type { get; set; } = "pattern";

        [CommandOption("-p|--project <project>")]
        [Description("Project name to associate with this observation")]
        public string? Project { get; set; }

        [CommandOption("--tags <tags>")]
        [Description("Comma-separated tags")]
        public string? Tags { get; set; }
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (!ObservationType.IsValid(settings.Type))
        {
            var valid = string.Join(", ", ObservationType.All);
            return ValidationResult.Error($"Unknown type '{settings.Type}'. Valid types: {valid}");
        }
        return ValidationResult.Success();
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var session = await _sessions.GetActiveSessionAsync(settings.Project)
            ?? await _sessions.CreateSessionAsync(settings.Project);

        var tags = settings.Tags?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? [];

        var obs = await _memory.SaveObservationAsync(
            session.Id,
            settings.Title,
            settings.Type,
            settings.Content,
            settings.Project,
            tags);

        var panel = new Panel(
            $"[bold]{Markup.Escape(obs.Title)}[/]\n" +
            $"[dim]Type:[/]    {obs.Type}\n" +
            $"[dim]ID:[/]      {obs.Id:N}\n" +
            (obs.Project != null ? $"[dim]Project:[/] {Markup.Escape(obs.Project)}\n" : "") +
            (tags.Length > 0 ? $"[dim]Tags:[/]    {string.Join(", ", tags.Select(Markup.Escape))}" : ""))
        {
            Header = new PanelHeader("[green] ✓ Observation Saved [/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = Style.Parse("green"),
            Padding = new Padding(1, 0),
        };

        AnsiConsole.Write(panel);
        return 0;
    }
}
