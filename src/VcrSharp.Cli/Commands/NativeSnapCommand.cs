using System.ComponentModel;
using System.Diagnostics;
using Spectre.Console;
using Spectre.Console.Cli;
using VcrSharp.Core.Rendering;
using VcrSharp.Core.Session;
using VcrSharp.Core.Settings;
using VcrSharp.Infrastructure.Rendering;
using VcrSharp.Infrastructure.Terminal;

namespace VcrSharp.Cli.Commands;

/// <summary>
/// [experimental] Browserless static SVG: run a command in an in-process pseudoconsole (ConPTY),
/// parse its VT output into a cell grid, and render it with the existing SvgRenderer — no ttyd, no
/// Chromium. The proof that the SVG path never needed a browser.
/// </summary>
[Description("[experimental] Browserless static SVG via in-process PTY (no ttyd/Chromium)")]
public sealed class NativeSnapCommand : AsyncCommand<NativeSnapCommand.Settings>
{
    public sealed class Settings : DirectCaptureSettings
    {
        [CommandOption("--cwd <DIR>")]
        [Description("Working directory for the command")]
        public string? WorkingDirectory { get; init; }

        [CommandOption("--animate")]
        [Description("Capture an animated SVG (poll the grid at --framerate) instead of a final-frame snapshot")]
        public bool Animate { get; init; }

        [CommandOption("--framerate <FPS>")]
        [Description("Frames per second when --animate is set (default 30)")]
        public int? Framerate { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            AnsiConsole.MarkupLine("[bold red]Error:[/] native-snap currently requires Windows (ConPTY). The Unix PTY backend is not wired up yet.");
            return 1;
        }

        var outputPath = settings.Output ?? "native.svg";
        if (!outputPath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            outputPath = Path.ChangeExtension(outputPath, ".svg");

        var cols = settings.Cols ?? 80;
        var rows = settings.Rows ?? 24;

        var options = new SessionOptions
        {
            Cols = cols,
            Rows = rows,
            FontSize = settings.FontSize ?? 22,
            Theme = settings.Theme is null ? BuiltinThemes.Default : (BuiltinThemes.GetByName(settings.Theme) ?? BuiltinThemes.Default),
            DisableCursor = settings.DisableCursor,
            TransparentBackground = settings.TransparentBackground,
            FitToContent = true, // a snapshot of finished output crops to content (Size fit)
        };

        try
        {
            var sw = Stopwatch.StartNew();

            if (settings.Animate)
                return await RenderAnimatedAsync(settings, options, outputPath, cols, rows, sw, cancellationToken);

            var content = await new NativeTerminalRenderer(cols, rows)
                .RunAndSnapshotAsync(settings.Command, settings.WorkingDirectory, cancellationToken: cancellationToken);

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var renderer = new SvgRenderer(options);
            if (options.FitToContent)
            {
                var extent = ContentExtent.Measure(content);
                renderer.SetContentExtent(extent.Cols, extent.Rows);
            }

            await renderer.RenderStaticAsync(outputPath, content, cancellationToken);
            sw.Stop();

            var size = new FileInfo(outputPath).Length / 1024.0;
            AnsiConsole.MarkupLine("[green]✓[/] Rendered with [bold]no browser and no ttyd[/] (in-process ConPTY + VT parser)");
            AnsiConsole.MarkupLineInterpolated($"  [dim]•[/] {Path.GetFileName(outputPath)} ({size:F1} KB) in {sw.ElapsedMilliseconds} ms");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[bold red]Error:[/] {ex.Message}");
            if (ex.InnerException != null)
                AnsiConsole.MarkupLineInterpolated($"[dim]{ex.InnerException.Message}[/]");
            return 1;
        }
    }

    private static async Task<int> RenderAnimatedAsync(Settings settings, SessionOptions options,
        string outputPath, int cols, int rows, Stopwatch sw, CancellationToken cancellationToken)
    {
        var fps = settings.Framerate is > 0 ? settings.Framerate.Value : 30;

        var capture = await new NativeTerminalRenderer(cols, rows)
            .RunAndCaptureAsync(settings.Command, fps, settings.WorkingDirectory, cancellationToken: cancellationToken);

        if (capture.States.Count == 0)
        {
            AnsiConsole.MarkupLine("[bold red]Error:[/] the command produced no terminal output to record.");
            return 1;
        }

        // Drop leading-blank frames and collapse a trailing static tail so a looping SVG starts on
        // content, then rebaseline timestamps; total duration runs to the true end so the last frame holds.
        var (keepStart, keepEnd) = ContentAnalysis.TrimBlankLoopRange(capture.States.Select(s => s.Content).ToList());
        var kept = capture.States.Skip(keepStart).Take(keepEnd - keepStart + 1).ToList();
        var baseline = kept[0].TimestampSeconds;
        var states = kept
            .Select(s => new TerminalStateWithTime { Content = s.Content, TimestampSeconds = s.TimestampSeconds - baseline })
            .ToList();
        var totalDuration = Math.Max(states[^1].TimestampSeconds, capture.TotalSeconds - baseline);

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var renderer = new SvgRenderer(options);
        if (options.FitToContent)
        {
            var extent = ContentExtent.Union(states.Select(s => s.Content));
            renderer.SetContentExtent(extent.Cols, extent.Rows);
        }

        await renderer.RenderAnimatedAsync(outputPath, states, totalDuration, cancellationToken);
        sw.Stop();

        var size = new FileInfo(outputPath).Length / 1024.0;
        AnsiConsole.MarkupLine("[green]✓[/] Recorded an [bold]animated SVG with no browser and no ttyd[/] (ConPTY + VT parser, polled at framerate)");
        AnsiConsole.MarkupLineInterpolated($"  [dim]•[/] {Path.GetFileName(outputPath)} ({size:F1} KB), {states.Count} frames over {totalDuration:F1}s @ {fps}fps in {sw.ElapsedMilliseconds} ms");
        return 0;
    }
}
