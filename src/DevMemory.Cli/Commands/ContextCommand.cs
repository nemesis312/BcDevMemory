using System.ComponentModel;
using DevMemory.Core.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DevMemory.Cli.Commands;

internal sealed class ContextCommand : AsyncCommand<ContextCommand.Settings>
{
    private readonly ISessionRepository _sessions;

    public ContextCommand(ISessionRepository sessions) => _sessions = sessions;

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[project]")]
        [Description("Project name to retrieve context for")]
        public string? Project { get; set; }

        [CommandOption("-l|--limit <limit>")]
        [Description("Number of recent sessions to include")]
        [DefaultValue(5)]
        public int Limit { get; set; } = 5;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (settings.Project is null)
        {
            AnsiConsole.MarkupLine("[yellow]Usage:[/] devmemory context <project>");
            return 1;
        }

        var ctx = await _sessions.GetProjectContextAsync(settings.Project, settings.Limit);

        if (ctx.RecentSessions.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]No sessions found for project:[/] {Markup.Escape(settings.Project)}");
            return 0;
        }

        AnsiConsole.Write(new Rule($"[cyan] Context — {Markup.Escape(settings.Project)} [/]")
        {
            Justification = Justify.Left,
            Style = Style.Parse("cyan"),
        });

        foreach (var session in ctx.RecentSessions)
        {
            var header = $"Session {session.SessionId.ToString()[..8]}";
            var lines = new List<string>();

            if (!string.IsNullOrEmpty(session.SessionGoal))
                lines.Add($"[dim]Goal:[/]    {Markup.Escape(session.SessionGoal)}");

            if (!string.IsNullOrEmpty(session.SessionSummary))
                lines.Add($"[dim]Summary:[/] {Markup.Escape(session.SessionSummary)}");

            lines.Add($"[dim]Started:[/] {session.StartedAt:yyyy-MM-dd HH:mm}  " +
                      $"[dim]Ended:[/] {(session.EndedAt.HasValue ? session.EndedAt.Value.ToString("HH:mm") : "active")}");
            lines.Add($"[dim]Observations:[/] {session.ObservationCount}");

            if (session.RecentObservations.Count > 0)
            {
                lines.Add("");
                lines.Add("[dim]Recent observations:[/]");
                foreach (var obs in session.RecentObservations.Take(3))
                    lines.Add($"  • [{obs.Type}] {Markup.Escape(obs.Title)}");
            }

            var panel = new Panel(string.Join("\n", lines))
            {
                Header = new PanelHeader($"[bold] {header} [/]"),
                Border = BoxBorder.Rounded,
                BorderStyle = Style.Parse("blue dim"),
                Padding = new Padding(1, 0),
            };

            AnsiConsole.Write(panel);
        }

        return 0;
    }
}
