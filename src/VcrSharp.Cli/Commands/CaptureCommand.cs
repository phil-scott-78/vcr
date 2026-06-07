using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using VcrSharp.Cli.Helpers;
using VcrSharp.Core.Logging;
using VcrSharp.Core.Session;
using VcrSharp.Infrastructure.Session;

namespace VcrSharp.Cli.Commands;

/// <summary>
/// Captures an animated SVG recording of a shell command's output (in-process PTY + VT engine).
/// Usage: vcr capture "command" -o output.svg
/// </summary>
[Description("Capture an animated SVG recording of a command's output")]
public class CaptureCommand : AsyncCommand<CaptureCommand.Settings>
{
    public class Settings : DirectCaptureSettings
    {
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        VcrLogger.Configure(settings.Verbose);

        try
        {
            // Determine output path (default to output.svg; capture is SVG-only)
            var outputPath = settings.Output ?? "output.svg";
            if (!outputPath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                outputPath = Path.ChangeExtension(outputPath, ".svg");

            // Build the command list (Set… + Output + Exec) and derive session options.
            var commands = CommandListBuilder.BuildCaptureCommands(settings, outputPath);
            var options = SessionOptions.FromCommands(commands);

            RecordingSession.Result? result = null;
            await AnsiConsole.Status()
                .StartAsync("Recording...", async ctx =>
                {
                    var progress = new Progress<string>(status => ctx.Status(status));
                    var session = new RecordingSession(options);
                    result = await session.RecordAsync(commands, [outputPath], options.Framerate, progress, cancellationToken);
                });

            AnsiConsole.MarkupLine("[green]✓[/] Recording captured");
            AnsiConsole.MarkupLineInterpolated($"[dim]Frames:[/] {result!.FrameCount}");
            AnsiConsole.MarkupLineInterpolated($"[dim]Duration:[/] {result.DurationSeconds:F2}s");

            foreach (var file in result.OutputFiles)
            {
                var fileSize = new FileInfo(file).Length / 1024.0;
                AnsiConsole.MarkupLineInterpolated($"  [dim]•[/] {Path.GetFileName(file)} ({fileSize:F1} KB)");
            }

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
}
