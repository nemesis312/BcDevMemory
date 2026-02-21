using System.ComponentModel;
using DevMemory.Core.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DevMemory.Cli.Commands;

internal sealed class SearchCommand : AsyncCommand<SearchCommand.Settings>
{
    private readonly ISearchService _search;

    public SearchCommand(ISearchService search) => _search = search;

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<query>")]
        [Description("Full-text search query")]
        public string Query { get; set; } = string.Empty;

        [CommandOption("-p|--project <project>")]
        [Description("Filter by project name")]
        public string? Project { get; set; }

        [CommandOption("-l|--limit <limit>")]
        [Description("Maximum results to return")]
        [DefaultValue(10)]
        public int Limit { get; set; } = 10;

        [CommandOption("-t|--type <type>")]
        [Description("Filter by observation type (bugfix, pattern, architecture-decision, etc.)")]
        public string? Type { get; set; }

        [CommandOption("--tags <tags>")]
        [Description("Filter by tags — comma-separated, matches any")]
        public string? Tags { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var tags = settings.Tags?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var results = (await _search.SearchAsync(
            settings.Query, settings.Project, settings.Limit,
            settings.Type, tags, cancellationToken)).ToList();
        sw.Stop();

        if (results.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]No results found for:[/] [italic]{Markup.Escape(settings.Query)}[/]");
            return 0;
        }

        AnsiConsole.Write(new Rule($"[cyan] Search Results — \"{Markup.Escape(settings.Query)}\" [/]")
        {
            Justification = Justify.Left,
            Style = Style.Parse("cyan"),
        });

        foreach (var obs in results)
        {
            var age = FormatAge(obs.CreatedAt);
            var rankStr = obs.Rank.HasValue ? $"Rank: {obs.Rank:F2}" : string.Empty;
            var preview = obs.ContentPreview ?? obs.Content[..Math.Min(200, obs.Content.Length)];

            var content =
                $"[bold cyan]{Markup.Escape(obs.Title)}[/]\n" +
                $"[dim]Type:[/] {obs.Type}  [dim]|[/]  {rankStr}  [dim]|[/]  {age}\n" +
                (obs.Project != null ? $"[dim]Project:[/] {Markup.Escape(obs.Project)}\n" : "") +
                $"\n{Markup.Escape(preview)}\n" +
                $"\n[dim]devmemory get {obs.Id:N}[/]";

            var panel = new Panel(content)
            {
                Border = BoxBorder.Rounded,
                BorderStyle = Style.Parse("dim"),
                Padding = new Padding(1, 0),
            };

            AnsiConsole.Write(panel);
        }

        AnsiConsole.Write(new Rule());
        AnsiConsole.MarkupLine($"[dim]Found {results.Count} result(s) in {sw.ElapsedMilliseconds}ms[/]");
        return 0;
    }

    private static string FormatAge(DateTime createdAt)
    {
        var diff = DateTime.UtcNow - createdAt.ToUniversalTime();
        return diff.TotalDays switch
        {
            < 1 when diff.TotalHours < 1 => $"{(int)diff.TotalMinutes}m ago",
            < 1 => $"{(int)diff.TotalHours}h ago",
            < 30 => $"{(int)diff.TotalDays}d ago",
            < 365 => $"{(int)(diff.TotalDays / 30)}mo ago",
            _ => $"{(int)(diff.TotalDays / 365)}y ago",
        };
    }
}
