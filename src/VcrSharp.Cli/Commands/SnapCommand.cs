using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using VcrSharp.Cli.Helpers;
using VcrSharp.Core.Logging;
using VcrSharp.Core.Session;
using VcrSharp.Infrastructure.Playwright;
using VcrSharp.Infrastructure.Processes;
using VcrSharp.Infrastructure.Session;

namespace VcrSharp.Cli.Commands;

/// <summary>
/// Captures a static SVG screenshot after a shell command completes.
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
            // Validate dependencies (ttyd required, FFmpeg not needed for SVG)
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

            // Determine output path (default to output.svg)
            var outputPath = settings.Output ?? "output.svg";

            // Ensure SVG extension for snap command
            if (!outputPath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
                outputPath = Path.ChangeExtension(outputPath, ".svg");
            }

            // Build command list programmatically
            var commands = CommandListBuilder.BuildSnapCommands(settings, outputPath);

            // Extract session options from commands
            var options = SessionOptions.FromCommands(commands);

            // Ensure Playwright browsers are installed
            await PlaywrightBrowser.EnsureBrowsersInstalled();

            // Record using VcrSession (reusing existing infrastructure)
            RecordingResult? result = null;
            await AnsiConsole.Status()
                .StartAsync("Capturing...", async ctx =>
                {
                    var progress = new Progress<string>(status => ctx.Status(status));

                    await using var session = new VcrSession(options);
                    result = await session.RecordAsync(commands, progress, cancellationToken);
                });

            // Display results
            AnsiConsole.MarkupLine("[green]✓[/] Snapshot captured");

            if (result!.ScreenshotFiles.Count > 0)
            {
                foreach (var file in result.ScreenshotFiles)
                {
                    var fileName = Path.GetFileName(file);
                    var fileSize = new FileInfo(file).Length / 1024.0;
                    AnsiConsole.MarkupLineInterpolated($"  [dim]•[/] {fileName} ({fileSize:F1} KB)");
                }
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
}
