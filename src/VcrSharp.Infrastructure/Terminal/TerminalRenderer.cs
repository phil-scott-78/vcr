using System.Diagnostics;
using System.Text;
using VcrSharp.Core.Rendering;
using VcrSharp.Terminal;

namespace VcrSharp.Infrastructure.Terminal;

/// <summary>
/// Run-and-snapshot helper: runs a command in an in-process pseudoconsole (ConPTY on Windows,
/// <c>posix_openpt</c>+<c>posix_spawn</c> on Unix), feeds its VT output through <see cref="VtScreen"/>, and
/// snapshots the settled cell grid to a <see cref="TerminalContent"/>. The caller (CLI) renders the
/// snapshot with the SvgRenderer.
/// </summary>
public sealed class TerminalRenderer
{
    private readonly int _cols;
    private readonly int _rows;

    public TerminalRenderer(int cols, int rows)
    {
        _cols = Math.Max(1, cols);
        _rows = Math.Max(1, rows);
    }

    /// <summary>
    /// Starts <paramref name="command"/> in a PTY. On Windows it runs through PowerShell; on Unix through
    /// <c>/bin/sh -c</c> — either way shell builtins / pipelines / `dotnet run` work, and the shell exits
    /// so the PTY reaches EOF. TERM/COLORTERM nudge Spectre to emit 256/truecolor SGR.
    /// </summary>
    private IPtyProcess StartPty(string command, string? workingDirectory)
    {
        var env = new Dictionary<string, string>
        {
            ["TERM"] = "xterm-256color",
            ["COLORTERM"] = "truecolor",
        };
        var windowsCommandLine = $"pwsh -NoLogo -NoProfile -Command \"{command.Replace("\"", "`\"")}\"";
        string[] unixArgv = ["/bin/sh", "-c", command];
        return PtyProcess.Start(windowsCommandLine, unixArgv, _cols, _rows, env, workingDirectory);
    }

    /// <summary>
    /// Runs <paramref name="command"/> (via <c>pwsh -Command</c>) to completion in a ConPTY and returns
    /// the final screen as a <see cref="TerminalContent"/>. Falls back to capturing whatever has been
    /// produced if the command outruns <paramref name="timeout"/>.
    /// </summary>
    public Task<TerminalContent> RunAndSnapshotAsync(string command, string? workingDirectory = null,
        TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        var maxMs = (int)(timeout ?? TimeSpan.FromSeconds(60)).TotalMilliseconds;

        var pty = StartPty(command, workingDirectory);
        var screen = new VtScreen(_cols, _rows);

        // Drain on a background thread: ConPTY only EOFs once the pseudoconsole is closed, so we read
        // until then, wait for the child to exit, then close to flush the tail and finish the drain.
        // Optional raw-stream dump for debugging the ConPTY VT output.
        var dumpPath = Environment.GetEnvironmentVariable("VCR_PTY_DUMP");
        var dump = string.IsNullOrEmpty(dumpPath) ? null : new StringBuilder();

        var decoder = Encoding.UTF8.GetDecoder();
        var ptyOutput = pty.Output;
        var readTask = Task.Run(() =>
        {
            var bytes = new byte[8192];
            var chars = new char[8192];
            int n;
            while ((n = ptyOutput.Read(bytes, 0, bytes.Length)) > 0)
            {
                var count = decoder.GetChars(bytes, 0, n, chars, 0);
                if (count > 0)
                {
                    var text = new string(chars, 0, count);
                    screen.Feed(text);
                    dump?.Append(text);
                }
            }
        }, cancellationToken);

        pty.WaitForExit(maxMs);
        pty.CloseChild();           // flush tail + signal EOF to the reader
        readTask.Wait(5000, cancellationToken);        // bounded: the drain should finish promptly after EOF

        var content = screen.ToTerminalContent();
        pty.Dispose();

        if (dump is not null && dumpPath is not null)
            File.WriteAllText(dumpPath, dump.ToString());

        return Task.FromResult(content);
    }

    /// <summary>The frames captured from an animated recording, plus the true
    /// wall-clock duration (which runs past the last visible change, so the final frame holds).</summary>
    public sealed record CaptureResult(IReadOnlyList<TerminalStateWithTime> States, double TotalSeconds);

    /// <summary>
    /// Runs <paramref name="command"/> in a ConPTY and polls the live <see cref="VtScreen"/> at
    /// <paramref name="framerate"/> fps, collecting a de-duplicated stream of timestamped grid snapshots
    /// (<see cref="TerminalStateWithTime"/>) — the exact input the animated <c>SvgRenderer</c> consumes.
    /// The drain thread feeds the parser; the poll loop snapshots it; a lock keeps the two from tearing
    /// a frame.
    /// </summary>
    public async Task<CaptureResult> RunAndCaptureAsync(string command, double framerate,
        string? workingDirectory = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        var maxMs = (int)(timeout ?? TimeSpan.FromSeconds(60)).TotalMilliseconds;
        var intervalMs = Math.Max(1, (int)Math.Round(1000.0 / Math.Max(1, framerate)));

        var pty = StartPty(command, workingDirectory);
        var screen = new VtScreen(_cols, _rows);
        var gate = new Lock();

        var decoder = Encoding.UTF8.GetDecoder();
        var ptyOutput = pty.Output;
        var readTask = Task.Run(() =>
        {
            var bytes = new byte[8192];
            var chars = new char[8192];
            int n;
            while ((n = ptyOutput.Read(bytes, 0, bytes.Length)) > 0)
            {
                var count = decoder.GetChars(bytes, 0, n, chars, 0);
                if (count > 0)
                {
                    var text = new string(chars, 0, count);
                    lock (gate) screen.Feed(text);
                }
            }
        }, cancellationToken);

        var sw = Stopwatch.StartNew();
        var states = new List<TerminalStateWithTime>();
        string? lastSignature = null;

        void Capture()
        {
            TerminalContent content;
            lock (gate) content = screen.ToTerminalContent();
            var signature = Signature(content);
            if (signature == lastSignature) return; // collapse consecutive identical frames
            lastSignature = signature;
            states.Add(new TerminalStateWithTime { Content = content, TimestampSeconds = sw.Elapsed.TotalSeconds });
        }

        while (!pty.HasExited && sw.ElapsedMilliseconds < maxMs)
        {
            Capture();
            try { await Task.Delay(intervalMs, cancellationToken); }
            catch (OperationCanceledException) { break; }
        }

        pty.CloseChild();      // flush the tail + signal EOF
        readTask.Wait(5000, cancellationToken);
        Capture();             // the settled final frame
        var total = sw.Elapsed.TotalSeconds;
        pty.Dispose();

        return new CaptureResult(states, total);
    }

    /// <summary>A cheap content fingerprint for frame de-duplication (cursor + every cell's glyph and
    /// rendering-relevant attributes).</summary>
    internal static string Signature(TerminalContent c)
    {
        var sb = new StringBuilder(c.Cols * c.Rows + 16);
        sb.Append(c.CursorVisible ? '1' : '0').Append(c.CursorX).Append(',').Append(c.CursorY).Append(';');
        foreach (var row in c.Cells)
            foreach (var cell in row)
            {
                // Every attribute the renderers actually draw must be in the fingerprint, or two frames
                // that differ ONLY in (e.g.) strike/overline/dim/conceal would be collapsed and dropped.
                sb.Append(cell.Character)
                  .Append(cell.ForegroundColor).Append('/')
                  .Append(cell.BackgroundColor).Append('/')
                  .Append(cell.UnderlineColor).Append('/')
                  .Append(cell.IsBold ? 'b' : '-')
                  .Append(cell.IsItalic ? 'i' : '-')
                  .Append(cell.IsUnderline ? 'u' : '-')
                  .Append((char)('0' + (cell.UnderlineStyle & 0xF)))
                  .Append(cell.IsReverse ? 'r' : '-')
                  .Append(cell.IsDim ? 'd' : '-')
                  .Append(cell.IsBlink ? 'k' : '-')
                  .Append(cell.IsConceal ? 'c' : '-')
                  .Append(cell.IsStrikethrough ? 's' : '-')
                  .Append(cell.IsOverline ? 'o' : '-')
                  .Append('|');
            }
        return sb.ToString();
    }
}
