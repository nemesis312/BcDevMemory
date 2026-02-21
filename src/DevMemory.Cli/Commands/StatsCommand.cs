using DevMemory.Core.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DevMemory.Cli.Commands;

internal sealed class StatsCommand : AsyncCommand
{
    private readonly IMemoryRepository _memory;

    public StatsCommand(IMemoryRepository memory) => _memory = memory;

    public override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var stats = await _memory.GetStatsAsync();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderStyle(Style.Parse("cyan"))
            .AddColumn(new TableColumn("[dim]Metric[/]"))
            .AddColumn(new TableColumn("[bold]Value[/]") { Alignment = Justify.Right });

        table.AddRow("Total Observations", $"[bold cyan]{stats.TotalObservations:N0}[/]");
        table.AddRow("Total Sessions",     $"[bold]{stats.TotalSessions:N0}[/]");
        table.AddRow("Active Sessions",    $"[bold green]{stats.ActiveSessions:N0}[/]");
        table.AddRow("Projects",           $"[bold]{stats.Projects:N0}[/]");

        if (stats.OldestObservation.HasValue)
            table.AddRow("Oldest Memory", $"[dim]{stats.OldestObservation.Value:yyyy-MM-dd}[/]");

        if (stats.NewestObservation.HasValue)
            table.AddRow("Newest Memory", $"[dim]{stats.NewestObservation.Value:yyyy-MM-dd HH:mm}[/]");

        var panel = new Panel(table)
        {
            Header = new PanelHeader("[cyan bold] DevMemory Statistics [/]"),
            Border = BoxBorder.Double,
            BorderStyle = Style.Parse("cyan"),
            Padding = new Padding(1, 0),
        };

        AnsiConsole.Write(panel);
        return 0;
    }
}
