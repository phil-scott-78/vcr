using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using VcrSharp.Core.Logging;
using VcrSharp.Core.Session;
using VcrSharp.Core.Settings;
using VcrSharp.Infrastructure.Playwright;
using VcrSharp.Infrastructure.Processes;
using VcrSharp.Infrastructure.Session;

namespace VcrSharp.Cli.Commands;

/// <summary>
/// Records an interactive shell session, capturing the user's keystrokes and writing them out as a
/// replayable <c>.tape</c> file. A terminal window opens for the user to type into; the session ends
/// when the user exits the shell (<c>exit</c> / Ctrl+D) or closes the window.
/// </summary>
[Description("Interactively record keystrokes in a real shell and generate a .tape file")]
public class RecordInteractiveCommand : AsyncCommand<RecordInteractiveCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[output-tape]")]
        [Description("Path to write the generated .tape file (default: recording.tape)")]
        public string? OutputTape { get; init; }

        [CommandOption("--shell <SHELL>")]
        [Description("Shell to record in (pwsh, powershell, cmd, bash, zsh, fish). Defaults to the platform shell.")]
        public string? Shell { get; init; }

        [CommandOption("--theme <THEME>")]
        [Description("Terminal theme to apply during recording")]
        public string? Theme { get; init; }

        [CommandOption("--cols <COLS>")]
        [Description("Terminal width in columns")]
        public int? Cols { get; init; }

        [CommandOption("--rows <ROWS>")]
        [Description("Terminal height in rows")]
        public int? Rows { get; init; }

        [CommandOption("--font-size <SIZE>")]
        [Description("Font size in pixels")]
        public int? FontSize { get; init; }

        [CommandOption("-v|--verbose")]
        [Description("Enable verbose logging")]
        public bool Verbose { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        VcrLogger.Configure(settings.Verbose);

        try
        {
            // ttyd is required; FFmpeg is not (this mode only writes a tape file).
            var missing = DependencyValidator.ValidateDependencies(requireTtyd: true, requireFfmpeg: false);
            if (missing.Count > 0)
            {
                AnsiConsole.MarkupLine("[bold red]Error:[/] Missing required dependencies:");
                foreach (var dep in missing)
                {
                    AnsiConsole.MarkupLineInterpolated($"  [red]✗[/] {dep}");
                }
                return 1;
            }

            var outputTape = settings.OutputTape ?? "recording.tape";

            var options = BuildOptions(settings);

            await PlaywrightBrowser.EnsureBrowsersInstalled();

            // Explain the popup-window workflow before launching it.
            var panel = new Panel(
                "A terminal window will open. [green]Type your commands there.[/]\n" +
                "When finished, type [yellow]exit[/] (or press [yellow]Ctrl+D[/]) — or just close the window.\n" +
                $"Your keystrokes will be saved to [blue]{Markup.Escape(outputTape)}[/].")
            {
                Header = new PanelHeader("vcr record"),
                Border = BoxBorder.Rounded
            };
            AnsiConsole.Write(panel);
            AnsiConsole.MarkupLine("[dim]Recording… finish in the terminal window to write the tape.[/]");

            var progress = new Progress<string>(status => VcrLogger.Logger.Information("{Status}", status));

            await using var session = new VcrSession(options);
            var result = await session.RecordInteractiveAsync(outputTape, progress, cancellationToken);

            if (File.Exists(result.TapeFile))
            {
                var fileSize = new FileInfo(result.TapeFile).Length / 1024.0;
                AnsiConsole.MarkupLineInterpolated($"[green]✓[/] Tape written: {Path.GetFileName(result.TapeFile)} ({fileSize:F1} KB)");
                AnsiConsole.MarkupLineInterpolated($"[dim]Replay it with:[/] vcr {result.TapeFile}");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]No tape file was written (no input captured).[/]");
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[bold red]Error:[/] {ex.Message}");
            if (ex.InnerException != null)
            {
                AnsiConsole.MarkupLineInterpolated($"[dim]{ex.InnerException.Message}[/]");
            }
            return 1;
        }
        finally
        {
            VcrLogger.Close();
        }
    }

    private static SessionOptions BuildOptions(Settings settings)
    {
        var options = new SessionOptions();

        if (!string.IsNullOrWhiteSpace(settings.Shell))
        {
            options.Shell = settings.Shell;
        }
        if (settings.Cols.HasValue)
        {
            options.Cols = settings.Cols;
        }
        if (settings.Rows.HasValue)
        {
            options.Rows = settings.Rows;
        }
        if (settings.FontSize.HasValue)
        {
            options.FontSize = settings.FontSize.Value;
        }
        if (!string.IsNullOrWhiteSpace(settings.Theme))
        {
            options.Theme = BuiltinThemes.GetByName(settings.Theme) ?? BuiltinThemes.Default;
        }

        return options;
    }
}
