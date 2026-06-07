using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using VcrSharp.Core.Logging;
using VcrSharp.Core.Recording;
using VcrSharp.Core.Session;
using VcrSharp.Core.Settings;
using VcrSharp.Infrastructure.Terminal;

namespace VcrSharp.Cli.Commands;

/// <summary>
/// Records an interactive shell session, capturing the user's keystrokes and writing them out as a
/// replayable <c>.tape</c> file. The shell runs in-process over a real PTY: you
/// type directly in this terminal, and the session ends when you exit the shell (<c>exit</c> / Ctrl+D).
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
        [Description("Terminal theme to record into the generated tape header")]
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
            var outputTape = settings.OutputTape ?? "recording.tape";
            var options = BuildOptions(settings);

            // Explain the in-terminal workflow before switching into the recorded shell.
            var panel = new Panel(
                "You're about to drop into a [green]recorded shell[/] right here.\n" +
                "Type your commands; when finished, type [yellow]exit[/] (or press [yellow]Ctrl+D[/]).\n" +
                $"Your keystrokes will be saved to [blue]{Markup.Escape(outputTape)}[/].")
            {
                Header = new PanelHeader("vcr record"),
                Border = BoxBorder.Rounded
            };
            AnsiConsole.Write(panel);

            var progress = new Progress<string>(status => VcrLogger.Logger.Information("{Status}", status));

            var recording = await InteractiveRecorder.RecordAsync(options, progress, cancellationToken);

            // Convert the captured input stream to tape text (shell-agnostic, pure function).
            var converterOptions = new InputToTapeOptions
            {
                Shell = options.Shell,
                DefaultShell = new SessionOptions().Shell,
                Header = options
            };
            var tape = InputToTapeConverter.Convert(recording.Events, converterOptions);

            if (recording.Events.Count == 0 || string.IsNullOrWhiteSpace(tape))
            {
                AnsiConsole.MarkupLine("[yellow]No tape file was written (no input captured).[/]");
                return 0;
            }

            var directory = Path.GetDirectoryName(Path.GetFullPath(outputTape));
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            // Write without the cancellation token so a cancelled session still saves its partial tape.
            await File.WriteAllTextAsync(outputTape, tape, CancellationToken.None);

            var fileSize = new FileInfo(outputTape).Length / 1024.0;
            AnsiConsole.MarkupLineInterpolated($"[green]✓[/] Tape written: {Path.GetFileName(outputTape)} ({fileSize:F1} KB)");
            AnsiConsole.MarkupLineInterpolated($"[dim]Replay it with:[/] vcr {outputTape}");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[bold red]Error:[/] {ex.Message}");
            if (ex.InnerException != null)
                AnsiConsole.MarkupLineInterpolated($"[dim]{ex.InnerException.Message}[/]");
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
            options.Shell = settings.Shell;
        if (settings.Cols.HasValue)
            options.Cols = settings.Cols;
        if (settings.Rows.HasValue)
            options.Rows = settings.Rows;
        if (settings.FontSize.HasValue)
            options.FontSize = settings.FontSize.Value;
        if (!string.IsNullOrWhiteSpace(settings.Theme))
            options.Theme = BuiltinThemes.GetByName(settings.Theme) ?? BuiltinThemes.Default;

        return options;
    }
}
