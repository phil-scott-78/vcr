using System.Diagnostics;
using System.Text;
using VcrSharp.Core.Parsing.Ast;
using VcrSharp.Core.Rendering;
using VcrSharp.Core.Session;
using VcrSharp.Infrastructure.Rendering;
using VcrSharp.Infrastructure.Terminal;
using VcrSharp.Terminal;

namespace VcrSharp.Infrastructure.Session;

/// <summary>
/// Plays a parsed tape through the browserless native backend: an interactive shell in an in-process
/// ConPTY, a <see cref="VtScreen"/> fed by a drain thread, and a poll loop that snapshots the grid at
/// the framerate. The existing tape commands (Type/Key/Modifier/Wait/Hide/Show/Copy/Paste/Screenshot)
/// run unchanged against <see cref="NativeTerminalPage"/>/<see cref="NativeFrameCapture"/>; output is
/// an animated SVG. No ttyd, no Chromium.
/// </summary>
public sealed class NativeRecordingSession(SessionOptions options)
{
    public sealed record Result(int FrameCount, double DurationSeconds,
        IReadOnlyList<string> OutputFiles, IReadOnlyList<string> UnsupportedOutputs);

    public async Task<Result> RecordAsync(List<ICommand> commands, IReadOnlyList<string> outputPaths,
        double framerate, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Native recording requires Windows (ConPTY).");

        var cols = options.Cols ?? 80;
        var rows = options.Rows ?? 24;
        var execCommands = commands.OfType<ExecCommand>().ToList();
        // Interactive (Type/Key) tapes need a live REPL to type into; pure-Exec showcase tapes run the
        // command non-interactively (like ttyd's startup script) so there is no prompt or echoed command line.
        var interactive = commands.Any(c => c is TypeCommand or KeyCommand or ModifierCommand or RunCommand);

        var env = new Dictionary<string, string> { ["TERM"] = "xterm-256color", ["COLORTERM"] = "truecolor" };
        foreach (var (k, v) in options.Environment) env[k] = v;

        var pty = ConPtyProcess.Start(BuildCommandLine(interactive, execCommands), cols, rows, env, options.WorkingDirectory);
        var screen = new VtScreen(cols, rows);
        var gate = new object();

        var decoder = Encoding.UTF8.GetDecoder();
        var readTask = Task.Run(() =>
        {
            var bytes = new byte[8192];
            var chars = new char[8192];
            int n;
            while ((n = pty.Output.Read(bytes, 0, bytes.Length)) > 0)
            {
                var count = decoder.GetChars(bytes, 0, n, chars, 0);
                if (count > 0)
                {
                    var text = new string(chars, 0, count);
                    lock (gate) screen.Feed(text);
                }
            }
        }, cancellationToken);

        var page = new NativeTerminalPage(pty, screen, gate, options);
        var frameCapture = new NativeFrameCapture(page, options);
        var state = new SessionState { IsCapturing = true };

        try
        {
            if (interactive)
            {
                progress?.Report("Starting shell...");
                await page.WaitForBufferContentAsync(3000);
                // Quiet PSReadLine's predictive/edit redraws so the typed demo reads cleanly.
                await page.TypeAsync("Set-PSReadLineOption -PredictionSource None 2>$null; Clear-Host", 0);
                await page.PressKeyAsync("Enter");
                await frameCapture.WaitForBufferStableAsync(TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(3), cancellationToken);

                // Exec commands run as live input alongside the typed demo.
                foreach (var exec in execCommands)
                {
                    await page.TypeAsync(exec.Command, 0);
                    await page.PressKeyAsync("Enter");
                }
            }

            progress?.Report("Recording (native, no browser)...");
            var states = new List<TerminalStateWithTime>();
            string? lastSignature = null;
            var sw = Stopwatch.StartNew();
            var intervalMs = Math.Max(1, (int)Math.Round(1000.0 / Math.Max(1, framerate)));
            using var stop = new CancellationTokenSource();

            var pollTask = Task.Run(async () =>
            {
                while (!stop.IsCancellationRequested)
                {
                    if (state.IsCapturing)
                    {
                        var content = page.Snapshot();
                        var signature = NativeTerminalRenderer.Signature(content);
                        if (signature != lastSignature)
                        {
                            lastSignature = signature;
                            states.Add(new TerminalStateWithTime { Content = content, TimestampSeconds = sw.Elapsed.TotalSeconds });
                        }
                    }
                    try { await Task.Delay(intervalMs, stop.Token); }
                    catch (OperationCanceledException) { break; }
                }
            }, CancellationToken.None);

            var context = new Core.Parsing.Ast.ExecutionContext(options, state, page, frameCapture);
            foreach (var command in commands)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await command.ExecuteAsync(context, cancellationToken);
            }

            // Let output settle so the final command's tail is captured. Use the same inactivity/max
            // budget as the browser path, plus a minimum wait when Exec is present so we don't settle on
            // the pre-output prompt while a slow command (e.g. `dotnet run`) is still starting up.
            var minWait = execCommands.Count > 0 ? TimeSpan.FromSeconds(2) : TimeSpan.Zero;
            await frameCapture.WaitForBufferStableAsync(options.InactivityTimeout, options.MaxWaitForInactivity, minWait, cancellationToken);

            stop.Cancel();
            await pollTask;
            var totalSeconds = sw.Elapsed.TotalSeconds;

            progress?.Report("Encoding...");
            var written = new List<string>();
            var unsupported = new List<string>();
            var frames = 0;
            foreach (var outPath in outputPaths)
            {
                if (!outPath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                {
                    unsupported.Add(outPath); // GIF/MP4/PNG need rasterisation — not in the native path yet
                    continue;
                }

                if (options.StaticOutput)
                {
                    await RenderStaticAsync(page.Snapshot(), outPath, cancellationToken);
                    frames = 1;
                }
                else
                {
                    frames = await NativeSvgWriter.WriteAnimatedAsync(states, totalSeconds, options, outPath, cancellationToken);
                }
                written.Add(outPath);
            }

            return new Result(frames, totalSeconds, written, unsupported);
        }
        finally
        {
            pty.CloseChild();
            try { readTask.Wait(3000); } catch { /* ignore */ }
            pty.Dispose();
        }
    }

    private async Task RenderStaticAsync(TerminalContent content, string outPath, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(outPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var renderer = new SvgRenderer(options);
        // Crop to the measured content extent so the canvas hugs the output (matching the browser's
        // static path) instead of leaving the default-width canvas padded with blank space.
        var extent = ContentExtent.Measure(content);
        renderer.SetContentExtent(extent.Cols, extent.Rows);
        await renderer.RenderStaticAsync(outPath, content, cancellationToken);
    }

    private static string BuildCommandLine(bool interactive, List<ExecCommand> execCommands)
    {
        const string shell = "pwsh -NoLogo -NoProfile";
        if (interactive || execCommands.Count == 0) return shell;
        // Run the Exec command(s) directly (no REPL) — output only, no prompt, no echoed command line.
        var joined = string.Join("; ", execCommands.Select(e => e.Command));
        return $"{shell} -Command \"{joined.Replace("\"", "`\"")}\"";
    }
}
