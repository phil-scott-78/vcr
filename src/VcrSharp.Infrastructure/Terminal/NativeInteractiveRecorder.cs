using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using VcrSharp.Core.Recording;
using VcrSharp.Core.Session;

namespace VcrSharp.Infrastructure.Terminal;

/// <summary>
/// Browserless interactive recorder: runs a child shell in an in-process PTY (ConPTY on Windows,
/// <c>posix_openpt</c> on Unix), puts the host console into raw/VT pass-through mode, and pumps bytes
/// both ways so the user types into a real shell. Every input chunk is also recorded as an
/// <see cref="InputEvent"/> (with a real timestamp) for <see cref="InputToTapeConverter"/> — the same
/// shell-agnostic input stream the old browser path captured via xterm.js <c>onData</c>, now with no
/// ttyd and no Chromium. The session ends when the shell exits (the user types <c>exit</c>/Ctrl+D).
/// </summary>
public sealed class NativeInteractiveRecorder
{
    public sealed record Result(IReadOnlyList<InputEvent> Events, TimeSpan Duration);

    public static async Task<Result> RecordAsync(SessionOptions options,
        IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var (cols, rows) = ResolveSize(options);

        var defaultShell = OperatingSystem.IsWindows() ? "pwsh" : "bash";
        var shellConfig = ShellConfiguration.GetConfiguration(
            string.IsNullOrWhiteSpace(options.Shell) ? defaultShell : options.Shell);
        var parts = shellConfig.BuildTtydCommand();
        var windowsCmd = string.Join(" ", parts.Select((p, i) => i > 0 && p.Contains(' ') ? $"\"{p}\"" : p));

        var env = new Dictionary<string, string> { ["TERM"] = "xterm-256color", ["COLORTERM"] = "truecolor" };
        foreach (var (k, v) in shellConfig.Environment) env[k] = v;
        foreach (var (k, v) in options.Environment) env[k] = v;

        var pty = PtyProcess.Start(windowsCmd, parts, cols, rows, env, options.WorkingDirectory);

        var events = new List<InputEvent>();
        var eventsLock = new object();
        var sw = Stopwatch.StartNew();
        var childExited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var rawMode = ConsoleRawMode.Enter();
        try
        {
            // Output pump: child VT stream -> host console (so the user sees the live shell).
            var stdout = Console.OpenStandardOutput();
            var ptyOutput = pty.Output;
            var outputPump = new Thread(() =>
            {
                var buf = new byte[8192];
                try
                {
                    int n;
                    while ((n = ptyOutput.Read(buf, 0, buf.Length)) > 0)
                    {
                        stdout.Write(buf, 0, n);
                        stdout.Flush();
                    }
                }
                catch { /* stream torn down */ }
                finally { childExited.TrySetResult(); }
            }) { IsBackground = true, Name = "vcr-record-out" };

            // Input pump: host stdin -> child, recording each chunk as a timestamped InputEvent. A real
            // multi-byte keystroke (e.g. an arrow's ESC [ A, or Alt+char) arrives in one read, so escape
            // sequences and Alt combos stay intact for the converter.
            var stdin = Console.OpenStandardInput();
            var ptyInput = pty.Input;
            var inputPump = new Thread(() =>
            {
                var buf = new byte[4096];
                var chars = new char[4096];
                var decoder = Encoding.UTF8.GetDecoder();
                try
                {
                    int n;
                    while ((n = stdin.Read(buf, 0, buf.Length)) > 0)
                    {
                        try { ptyInput.Write(buf, 0, n); ptyInput.Flush(); } catch { break; }
                        var c = decoder.GetChars(buf, 0, n, chars, 0);
                        if (c > 0)
                        {
                            var data = new string(chars, 0, c);
                            lock (eventsLock) events.Add(new InputEvent(data, sw.Elapsed));
                        }
                        if (childExited.Task.IsCompleted) break;
                    }
                }
                catch { /* ignore */ }
            }) { IsBackground = true, Name = "vcr-record-in" };

            outputPump.Start();
            inputPump.Start();

            progress?.Report("Recording — type in this shell. Type `exit` (or Ctrl+D) to finish.");

            // Wait until the child shell exits (user typed exit / Ctrl+D) or the op is cancelled.
            using (cancellationToken.Register(() => childExited.TrySetResult()))
                await childExited.Task;

            sw.Stop();

            // Drain a brief tail so the last output is flushed, then tear the PTY down.
            pty.CloseChild();
            var pumpStopped = false;
            try { pumpStopped = outputPump.Join(1000); } catch { /* ignore */ }
            // Only emit the SGR reset once the output pump has actually stopped, so we never interleave
            // writes to the shared console stream with the pump's final flush.
            if (pumpStopped)
                try { stdout.Write(Encoding.ASCII.GetBytes("\x1b[0m")); stdout.Flush(); } catch { /* ignore */ }
        }
        finally
        {
            rawMode.Dispose();
            pty.Dispose();
        }

        List<InputEvent> captured;
        lock (eventsLock) captured = new List<InputEvent>(events);
        return new Result(captured, sw.Elapsed);
    }

    private static (int cols, int rows) ResolveSize(SessionOptions options)
    {
        int cols = options.Cols ?? 0, rows = options.Rows ?? 0;
        try
        {
            if (cols <= 0) cols = Console.WindowWidth;
            if (rows <= 0) rows = Console.WindowHeight;
        }
        catch { /* no console window (redirected) — fall through to defaults */ }
        if (cols <= 0) cols = 80;
        if (rows <= 0) rows = 24;
        return (cols, rows);
    }
}

/// <summary>
/// Puts the host console into raw / VT pass-through mode for the duration of an interactive recording,
/// restoring the original modes on <see cref="Dispose"/>. Windows toggles the console-mode flags
/// directly; Unix shells out to <c>stty</c> (robust across Linux/macOS without marshaling termios).
/// </summary>
internal sealed class ConsoleRawMode : IDisposable
{
    private readonly Action _restore;
    private bool _disposed;

    private ConsoleRawMode(Action restore) => _restore = restore;

    public static ConsoleRawMode Enter()
    {
        try
        {
            return OperatingSystem.IsWindows() ? EnterWindows() : EnterUnix();
        }
        catch
        {
            // If raw mode can't be established (e.g. redirected stdio), record in cooked mode rather than fail.
            return new ConsoleRawMode(() => { });
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _restore(); } catch { /* best-effort restore */ }
    }

    // ---- Windows ----

    private const int STD_INPUT_HANDLE = -10;
    private const int STD_OUTPUT_HANDLE = -11;
    private const uint ENABLE_PROCESSED_INPUT = 0x0001;
    private const uint ENABLE_LINE_INPUT = 0x0002;
    private const uint ENABLE_ECHO_INPUT = 0x0004;
    private const uint ENABLE_VIRTUAL_TERMINAL_INPUT = 0x0200;
    private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
    private const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;

    private static ConsoleRawMode EnterWindows()
    {
        var hIn = GetStdHandle(STD_INPUT_HANDLE);
        var hOut = GetStdHandle(STD_OUTPUT_HANDLE);
        GetConsoleMode(hIn, out var inMode);
        GetConsoleMode(hOut, out var outMode);

        var newIn = (inMode & ~(ENABLE_LINE_INPUT | ENABLE_ECHO_INPUT | ENABLE_PROCESSED_INPUT))
                    | ENABLE_VIRTUAL_TERMINAL_INPUT;
        var newOut = outMode | ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN;
        SetConsoleMode(hIn, newIn);
        SetConsoleMode(hOut, newOut);

        return new ConsoleRawMode(() =>
        {
            SetConsoleMode(hIn, inMode);
            SetConsoleMode(hOut, outMode);
        });
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    // ---- Unix (via stty on the controlling terminal) ----

    private static ConsoleRawMode EnterUnix()
    {
        var saved = Stty("-g")?.Trim();
        Stty("raw -echo");
        return new ConsoleRawMode(() => Stty(string.IsNullOrEmpty(saved) ? "sane" : saved));
    }

    private static string? Stty(string args)
    {
        // Inherit our stdin so stty acts on the controlling terminal; capture stdout for `stty -g`.
        var psi = new ProcessStartInfo("/bin/sh", $"-c \"stty {args} < /dev/tty\"")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        using var p = Process.Start(psi);
        if (p is null) return null;
        var outp = p.StandardOutput.ReadToEnd();
        p.WaitForExit(2000);
        return outp;
    }
}
