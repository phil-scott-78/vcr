using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using VcrSharp.Cli.Helpers;
using VcrSharp.Core.Config;
using VcrSharp.Core.Logging;
using VcrSharp.Core.Parsing;
using VcrSharp.Core.Parsing.Ast;
using VcrSharp.Core.Session;
using VcrSharp.Infrastructure.Session;

namespace VcrSharp.Cli.Commands;

/// <summary>
/// [experimental] Plays a .tape file through the browserless native backend (in-process ConPTY + VT
/// parser) and renders an animated SVG — Type/Key/Wait/Hide/Exec all run against the real shell over a
/// pseudo-console, no ttyd and no Chromium. SVG output only for now.
/// </summary>
[Description("[experimental] Play a .tape to an animated SVG with no ttyd/Chromium (native ConPTY)")]
public sealed class NativePlayCommand : AsyncCommand<NativePlayCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<tape-file>")]
        [Description("Path to the .tape file")]
        public required string TapeFile { get; init; }

        [CommandOption("-o|--output <FILE>")]
        [Description("Output .svg path (overrides/append to the tape's Output)")]
        public string[]? Output { get; init; }

        [CommandOption("--framerate <FPS>")]
        [Description("Capture framerate (default: the tape's Framerate, else 50)")]
        public int? Framerate { get; init; }

        [CommandOption("-v|--verbose")]
        public bool Verbose { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        VcrLogger.Configure(settings.Verbose);

        if (!OperatingSystem.IsWindows())
        {
            AnsiConsole.MarkupLine("[bold red]Error:[/] native playback currently requires Windows (ConPTY). Use the browser path on other platforms.");
            return 1;
        }
        if (!File.Exists(settings.TapeFile))
        {
            AnsiConsole.MarkupLineInterpolated($"[bold red]Error:[/] Tape file not found: {settings.TapeFile}");
            return 1;
        }

        try
        {
            var parser = new TapeParser();
            var commands = await parser.ParseFileAsync(settings.TapeFile);
            commands = PresetResolver.ResolveWithDiscovery(commands, settings.TapeFile);

            var options = SessionOptions.FromCommands(commands);

            var outputs = commands.OfType<OutputCommand>().Select(o => o.FilePath).ToList();
            if (settings.Output is { Length: > 0 }) outputs.AddRange(settings.Output);
            if (outputs.Count == 0) outputs.Add("output.svg");

            string[] supportedExts = [".svg", ".gif", ".mp4", ".webm", ".png"];
            var renderable = outputs.Where(o => supportedExts.Contains(Path.GetExtension(o).ToLowerInvariant())).Distinct().ToList();
            foreach (var o in outputs.Where(o => !renderable.Contains(o)).Distinct())
                AnsiConsole.MarkupLineInterpolated($"[yellow]⚠[/] Skipping [bold]{Path.GetFileName(o)}[/] — native supports .svg/.gif/.mp4/.webm/.png.");

            if (renderable.Count == 0)
            {
                AnsiConsole.MarkupLine("[bold red]Error:[/] no renderable output. Pass `-o out.svg` (or .gif/.mp4/.webm/.png) or add an `Output` to the tape.");
                return 1;
            }

            var framerate = settings.Framerate ?? options.Framerate;

            NativeRecordingSession.Result? result = null;
            await AnsiConsole.Status().StartAsync("Initializing...", async ctx =>
            {
                var progress = new Progress<string>(s => ctx.Status(s));
                var session = new NativeRecordingSession(options);
                result = await session.RecordAsync(commands, renderable, framerate, progress, cancellationToken);
            });

            AnsiConsole.MarkupLine("[green]✓[/] Played tape with [bold]no ttyd and no Chromium[/] (ConPTY + VT parser)");
            AnsiConsole.MarkupLineInterpolated($"[dim]Frames:[/] {result!.FrameCount}   [dim]Duration:[/] {result.DurationSeconds:F2}s   [dim]@[/] {framerate}fps");
            foreach (var file in result.OutputFiles)
            {
                var kb = new FileInfo(file).Length / 1024.0;
                AnsiConsole.MarkupLineInterpolated($"  [dim]•[/] {Path.GetFileName(file)} ({kb:F1} KB)");
            }
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
            if (ex.InnerException != null)
                AnsiConsole.MarkupLineInterpolated($"[dim]{ex.InnerException.Message}[/]");
            return 1;
        }
        finally
        {
            VcrLogger.Close();
        }
    }
}
