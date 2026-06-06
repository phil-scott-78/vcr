using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using VcrSharp.Core.Config;

namespace VcrSharp.Cli.Commands;

/// <summary>
/// Migrates a directory of legacy tapes to the config-layer model: mines the copy-pasted house
/// style into a single <c>vcr.toml</c> preset and rewrites each tape to <c>Use</c> it. Dry-run by
/// default — every rewrite is equivalence-checked (same realized config + actions) and only safe,
/// changed tapes are written when <c>--write</c> is passed.
/// </summary>
public class MigrateCommand : AsyncCommand<MigrateCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<directory>")]
        [Description("Directory containing .tape files to migrate")]
        public string Directory { get; init; } = string.Empty;

        [CommandOption("--write")]
        [Description("Apply the migration (write vcr.toml and rewrite tapes). Default is a dry run.")]
        public bool Write { get; init; }

        [CommandOption("--preset <NAME>")]
        [Description("Name of the generated preset (default: doc)")]
        public string? Preset { get; init; }

        [CommandOption("--threshold <FRACTION>")]
        [Description("Fraction of tapes that must share a setting for it to move into the preset (default: 0.6)")]
        public double? Threshold { get; init; }

        [CommandOption("-r|--recursive")]
        [Description("Recurse into subdirectories")]
        public bool Recursive { get; init; }
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(settings.Directory))
        {
            AnsiConsole.MarkupLineInterpolated($"[bold red]Error:[/] Directory not found: {settings.Directory}");
            return Task.FromResult(1);
        }

        var searchOption = settings.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.GetFiles(settings.Directory, "*.tape", searchOption).OrderBy(f => f).ToList();
        if (files.Count == 0)
        {
            AnsiConsole.MarkupLineInterpolated($"[yellow]No .tape files found in[/] {settings.Directory}");
            return Task.FromResult(0);
        }

        var tapes = files.Select(f => (Path: f, Text: File.ReadAllText(f))).ToList();
        var options = new MigrateOptions
        {
            PresetName = settings.Preset ?? "doc",
            Threshold = settings.Threshold ?? 0.6,
        };

        MigrationPlan plan;
        try
        {
            plan = TapeMigrator.Plan(tapes, settings.Directory, options);
        }
        catch (VcrConfigException ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[bold red]Migration error:[/] {ex.Message}");
            return Task.FromResult(1);
        }

        // Generated vcr.toml
        AnsiConsole.Write(new Rule($"[bold]Generated[/] {Markup.Escape(Path.GetRelativePath(settings.Directory, plan.ConfigPath))}").LeftJustified());
        AnsiConsole.Write(new Panel(new Text(plan.ConfigToml.TrimEnd())).Border(BoxBorder.Rounded).Expand());

        if (plan.Clusters.Count > 0)
        {
            var profiles = string.Join("  ", plan.Clusters.Select(c => $"[bold]{Markup.Escape(c.Name)}[/] ({c.Paths.Count})"));
            AnsiConsole.MarkupLine($"[dim]Profiles:[/] {profiles}");
        }

        // Per-tape result table
        var table = new Table().Border(TableBorder.Rounded).Expand();
        table.AddColumn(new TableColumn("[bold]Tape[/]"));
        table.AddColumn(new TableColumn("[bold]Removed[/]").RightAligned());
        table.AddColumn(new TableColumn("[bold]Status[/]"));
        foreach (var r in plan.Rewrites)
        {
            var name = Path.GetFileName(r.Path);
            var status = r switch
            {
                { Changed: true, EquivalenceOk: true } => $"[green]✓ Use {Markup.Escape(r.PresetName ?? "")}[/]",
                { SkipReason: not null } => $"[yellow]• {Markup.Escape(r.SkipReason)}[/]",
                _ => "[dim]unchanged[/]",
            };
            table.AddRow(Markup.Escape(name), r.RemovedLines.Count.ToString(), status);
        }
        AnsiConsole.Write(table);

        // Sample before/after for the first couple of rewritten tapes
        foreach (var r in plan.Rewrites.Where(r => r is { Changed: true, EquivalenceOk: true }).Take(2))
        {
            AnsiConsole.Write(new Rule($"[bold]Diff[/] {Markup.Escape(Path.GetFileName(r.Path))}").LeftJustified());
            foreach (var removed in r.RemovedLines)
                AnsiConsole.MarkupLineInterpolated($"[red]- {removed}[/]");
            foreach (var added in r.AddedLines)
                AnsiConsole.MarkupLineInterpolated($"[green]+ {added}[/]");
        }

        // Summary
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLineInterpolated(
            $"[bold]{plan.Rewrites.Count}[/] tapes  •  [green]{plan.Rewritten}[/] to rewrite  •  [yellow]{plan.Skipped}[/] skipped  •  [bold]{plan.DuplicatedLinesRemoved}[/] duplicated Set lines removed");

        var drift = plan.Rewrites.Count(r => r.SkipReason == "equivalence drift (left untouched)");
        if (drift > 0)
            AnsiConsole.MarkupLineInterpolated($"[yellow]⚠ {drift} tape(s) left untouched due to equivalence drift.[/]");
        else
            AnsiConsole.MarkupLine("[green]✓ Every rewrite resolves to a byte-identical effective config and action sequence.[/]");

        if (!settings.Write)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Dry run. Re-run with[/] [bold]--write[/] [dim]to apply.[/]");
            return Task.FromResult(0);
        }

        // Apply
        File.WriteAllText(plan.ConfigPath, plan.ConfigToml);
        var written = 0;
        foreach (var r in plan.Rewrites.Where(r => r is { Changed: true, EquivalenceOk: true }))
        {
            File.WriteAllText(r.Path, r.NewText);
            written++;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLineInterpolated($"[green]✓[/] Wrote {Markup.Escape(Path.GetFileName(plan.ConfigPath))} and rewrote {written} tape(s).");
        return Task.FromResult(0);
    }
}
