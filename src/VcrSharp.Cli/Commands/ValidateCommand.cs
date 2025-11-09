using Spectre.Console;
using Spectre.Console.Cli;
using VcrSharp.Cli.Helpers;
using VcrSharp.Core.Parsing;

namespace VcrSharp.Cli.Commands;

/// <summary>
/// Command to validate a tape file.
/// </summary>
public class ValidateCommand : AsyncCommand<ValidateCommand.Settings>
{
    /// <summary>
    /// Settings for the validate command.
    /// </summary>
    public class Settings : CommandSettings
    {
        /// <summary>
        /// Gets or sets the tape file path.
        /// </summary>
        [CommandArgument(0, "<tape-file>")]
        public string TapeFile { get; set; } = string.Empty;
    }

    /// <summary>
    /// Executes the validate command.
    /// </summary>
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[bold blue]VcrSharp[/] - Tape Validator");
        AnsiConsole.WriteLine();

        // Validate tape file exists
        if (!File.Exists(settings.TapeFile))
        {
            AnsiConsole.MarkupLineInterpolated($"[bold red]Error:[/] Tape file not found: {settings.TapeFile}");
            return 1;
        }

        // Parse and validate the tape file
        try
        {
            AnsiConsole.MarkupLineInterpolated($"[dim]Parsing:[/] {settings.TapeFile}");

            var parser = new TapeParser();
            var commands = await parser.ParseFileAsync(settings.TapeFile);

            AnsiConsole.MarkupLineInterpolated($"[green]✓[/] Successfully parsed {commands.Count} command(s)");
            AnsiConsole.WriteLine();

            // Display summary
            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.AddColumn("[bold]Command Type[/]");
            table.AddColumn("[bold]Count[/]");

            var commandGroups = commands
                .GroupBy(c => c.GetType().Name.Replace("Command", ""))
                .OrderByDescending(g => g.Count());

            foreach (var group in commandGroups)
            {
                table.AddRow(group.Key, group.Count().ToString());
            }

            AnsiConsole.Write(table);

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[green]✓[/] Tape file is valid!");

            return 0;
        }
        catch (TapeParseException ex)
        {
            ErrorReporter.DisplayParseError(ex, settings.TapeFile);
            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[bold red]Error:[/] {ex.Message}");
            return 1;
        }
    }
}