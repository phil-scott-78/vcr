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
        var cols = options.Cols ?? 80;
        var rows = options.Rows ?? 24;
        var execCommands = commands.OfType<ExecCommand>().ToList();
        // Launch shape keys off Exec presence, NOT Type/Key presence (see ShouldUseBareRepl): an Exec tape
        // launches the command as the shell's hidden foreground process; a no-Exec tape types its own
        // commands into a bare REPL.
        var bareRepl = ShouldUseBareRepl(commands);

        // Honor `Set Shell` via the same ShellConfiguration the browser path uses, so bash/zsh/cmd/fish
        // tapes run the right shell with the same args, prompt, and env. The unspecified-shell default is
        // platform-appropriate: pwsh on Windows (where ConPTY lives), bash on Unix.
        var defaultShell = OperatingSystem.IsWindows() ? "pwsh" : "bash";
        var shellConfig = ShellConfiguration.GetConfiguration(string.IsNullOrWhiteSpace(options.Shell) ? defaultShell : options.Shell);

        var env = new Dictionary<string, string> { ["TERM"] = "xterm-256color", ["COLORTERM"] = "truecolor" };
        foreach (var (k, v) in shellConfig.Environment) env[k] = v;   // shell-specific (e.g. zsh PROMPT)
        foreach (var (k, v) in options.Environment) env[k] = v;       // tape Env overrides

        var pty = PtyProcess.Start(
            BuildCommandLine(shellConfig, bareRepl, execCommands),
            BuildUnixArgv(shellConfig, bareRepl, execCommands),
            cols, rows, env, options.WorkingDirectory);
        var screen = new VtScreen(cols, rows);
        var gate = new object();

        var page = new NativeTerminalPage(pty, screen, gate, options);
        var frameCapture = new NativeFrameCapture(page, options);
        var state = new SessionState { IsCapturing = true };

        // Event-driven capture: the drain thread snapshots after EVERY output chunk (not on a fixed
        // framerate timer), so no on-screen state is dropped — native sees every byte, so it must never
        // miss a frame the way periodic sampling does (e.g. rapid arrow-key navigation). Frames are
        // de-duplicated, so the stream is exactly the sequence of distinct screen states. `capturing`
        // gates it on after the startup setup so the shell init never enters the recording.
        var capturedFrames = new List<TerminalStateWithTime>();
        var framesLock = new object();
        var captureSw = new Stopwatch();
        var capturing = false;
        string? lastSignature = null;
        const int MaxFrames = 6000;

        var decoder = Encoding.UTF8.GetDecoder();
        var readTask = Task.Run(() =>
        {
            var bytes = new byte[8192];
            var chars = new char[8192];
            int n;
            while ((n = pty.Output.Read(bytes, 0, bytes.Length)) > 0)
            {
                var count = decoder.GetChars(bytes, 0, n, chars, 0);
                if (count == 0) continue;
                lock (gate) screen.Feed(new string(chars, 0, count));

                bool cap;
                lock (framesLock) cap = capturing;
                if (!cap || !state.IsCapturing) continue;

                var content = page.Snapshot();
                var signature = NativeTerminalRenderer.Signature(content);
                lock (framesLock)
                {
                    if (signature == lastSignature || capturedFrames.Count >= MaxFrames) continue;
                    lastSignature = signature;
                    capturedFrames.Add(new TerminalStateWithTime { Content = content, TimestampSeconds = captureSw.Elapsed.TotalSeconds });
                }
            }
        }, cancellationToken);

        try
        {
            if (bareRepl)
            {
                progress?.Report("Starting shell...");
                // No-Exec tape: we type the demo's commands into a live REPL. PSReadLine prediction is off and
                // the screen is cleared via the shell startup (see BuildCommandLine), so no setup leaks into
                // the capture — just wait for the prompt before the command loop starts typing.
                await page.WaitForBufferContentAsync(3000);
            }
            // Exec tape: the shell launches the Exec command directly (hidden, never echoed) and the launched
            // app receives our Type/Key input over the PTY. There is no prompt to wait for and nothing to type
            // here — the tape's own Wait/Sleep gate when the app is ready, exactly as the pure-showcase path.

            progress?.Report("Recording (native, no browser)...");
            lock (framesLock) { capturing = true; lastSignature = null; capturedFrames.Clear(); captureSw.Restart(); }

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

            // Add the settled final frame, then stop capturing and snapshot the frame list for encoding.
            var finalContent = page.Snapshot();
            List<TerminalStateWithTime> states;
            double totalSeconds;
            lock (framesLock)
            {
                var sig = NativeTerminalRenderer.Signature(finalContent);
                if (sig != lastSignature && capturedFrames.Count < MaxFrames)
                    capturedFrames.Add(new TerminalStateWithTime { Content = finalContent, TimestampSeconds = captureSw.Elapsed.TotalSeconds });
                capturing = false;
                totalSeconds = captureSw.Elapsed.TotalSeconds;
                states = new List<TerminalStateWithTime>(capturedFrames);
            }

            progress?.Report("Encoding...");
            var written = new List<string>();
            var unsupported = new List<string>();
            var frames = 0;
            foreach (var outPath in outputPaths)
            {
                var ext = Path.GetExtension(outPath).ToLowerInvariant();
                switch (ext)
                {
                    case ".svg" when options.StaticOutput:
                        await RenderStaticAsync(page.Snapshot(), outPath, cancellationToken);
                        frames = 1;
                        written.Add(outPath);
                        break;
                    case ".svg":
                        frames = await NativeSvgWriter.WriteAnimatedAsync(states, totalSeconds, options, outPath, cancellationToken);
                        written.Add(outPath);
                        break;
                    case ".gif" or ".mp4" or ".webm" or ".png":
                        // Rasterize each captured frame and feed FFmpeg (browserless GIF/MP4/WebM/PNG).
                        frames = await NativeVideoWriter.WriteAsync(states, totalSeconds, options, outPath, cancellationToken);
                        written.Add(outPath);
                        break;
                    default:
                        unsupported.Add(outPath); // e.g. a frames/ directory — not handled natively
                        break;
                }
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

    /// <summary>
    /// Decides the launch shape. A bare interactive REPL is used ONLY when the tape has no Exec — there the
    /// typed command line IS the demo and must be shown. Any tape WITH Exec launches it as the shell's
    /// foreground command (hidden, never echoed) — whether it is a pure showcase OR then drives the launched
    /// app with Type/Key — so a "launch the app, then answer its prompts" tape never leaks the launch line.
    /// </summary>
    internal static bool ShouldUseBareRepl(IEnumerable<ICommand> commands)
        => !commands.OfType<ExecCommand>().Any();

    /// <summary>
    /// The Unix equivalent of <see cref="BuildCommandLine"/>: an argv vector for <c>posix_spawn</c> (no
    /// shell-string re-parsing). A bare REPL reuses the shell's exact invocation parts; otherwise the joined
    /// Exec command(s) run via the shell's execution flag and exit (the launched app inherits the PTY).
    /// </summary>
    internal static IReadOnlyList<string> BuildUnixArgv(ShellConfiguration config, bool bareRepl, List<ExecCommand> execCommands)
    {
        if (bareRepl)
            return config.BuildTtydCommand();

        if (execCommands.Count == 0)
            return [config.Name];

        var joined = string.Join("; ", execCommands.Select(e => e.Command));
        return [config.Name, config.ExecutionFlag, joined];
    }

    private static string BuildCommandLine(ShellConfiguration config, bool bareRepl, List<ExecCommand> execCommands)
    {
        if (bareRepl)
        {
            // No-Exec tape: reuse the shell's exact interactive invocation (flags + clean '> ' prompt +
            // init) — the same one the browser/ttyd path uses — so input and prompt match across backends.
            var parts = config.BuildTtydCommand();
            return string.Join(" ", parts.Select((p, i) => i > 0 && p.Contains(' ') ? $"\"{p}\"" : p));
        }

        if (execCommands.Count == 0) return config.Name;

        // Exec tape: run the Exec command(s) and exit — output only, no prompt, no echoed launch line. The
        // launched app inherits the PTY, so any Type/Key the tape sends afterward flows straight to it.
        var sep = config.Name.StartsWith("cmd", StringComparison.OrdinalIgnoreCase) ? " & " : "; ";
        var joined = string.Join(sep, execCommands.Select(e => e.Command));
        var quoted = $"\"{joined.Replace("\"", "`\"")}\"";
        return config.Name is "pwsh" or "powershell"
            ? $"{config.Name} -NoLogo -NoProfile {config.ExecutionFlag} {quoted}"
            : $"{config.Name} {config.ExecutionFlag} {quoted}";
    }
}
