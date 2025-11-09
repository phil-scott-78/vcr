using Spectre.Console;
using Spectre.Console.Cli;
using VcrSharp.Core.Settings;

namespace VcrSharp.Cli.Commands;

/// <summary>
/// Command to list available themes.
/// </summary>
public class ThemesCommand : Command<EmptyCommandSettings>
{
    /// <summary>
    /// Executes the themes command.
    /// </summary>
    public override int Execute(CommandContext context, EmptyCommandSettings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[bold blue]VcrSharp[/] - Available Themes");
        AnsiConsole.WriteLine();

        var table = new Table();
        table.AddColumn("Theme Name");
        table.AddColumn("Background");
        table.AddColumn("Foreground");

        foreach (var theme in BuiltinThemes.All)
        {
            table.AddRow(
                theme.Name,
                theme.Background,
                theme.Foreground);
        }

        AnsiConsole.Write(table);

        return 0;
    }
}