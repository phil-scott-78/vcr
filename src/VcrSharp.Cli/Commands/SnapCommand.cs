using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using VcrSharp.Cli.Helpers;
using VcrSharp.Core.Logging;
using VcrSharp.Core.Session;
using VcrSharp.Infrastructure.Session;

namespace VcrSharp.Cli.Commands;

/// <summary>
/// Captures a static SVG screenshot after a shell command completes, using the browserless native
/// backend (in-process PTY + VT engine — no ttyd, no Chromium).
/// Usage: vcr snap "command" -o output.svg
/// </summary>
[Description("Capture a static SVG screenshot of a command's final output")]
public class SnapCommand : AsyncCommand<SnapCommand.Settings>
{
    public class Settings : DirectCaptureSettings
    {
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        VcrLogger.Configure(settings.Verbose);

        try
        {
            // Determine output path (default to output.svg; snap is SVG-only)
            var outputPath = settings.Output ?? "output.svg";
            if (!outputPath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                outputPath = Path.ChangeExtension(outputPath, ".svg");

            // Build the command list (Set… + Exec + Screenshot) and derive session options.
            var commands = CommandListBuilder.BuildSnapCommands(settings, outputPath);
            var options = SessionOptions.FromCommands(commands);
            options.FitToContent = true; // a snapshot of finished output crops to content

            NativeRecordingSession.Result? result = null;
            await AnsiConsole.Status()
                .StartAsync("Capturing...", async ctx =>
                {
                    var progress = new Progress<string>(status => ctx.Status(status));
                    var session = new NativeRecordingSession(options);
                    // No animated Output — the Screenshot command writes the SVG and records it.
                    result = await session.RecordAsync(commands, [], options.Framerate, progress, cancellationToken);
                });

            AnsiConsole.MarkupLine("[green]✓[/] Snapshot captured [dim](no ttyd, no Chromium)[/]");
            foreach (var file in result!.ScreenshotFiles)
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
