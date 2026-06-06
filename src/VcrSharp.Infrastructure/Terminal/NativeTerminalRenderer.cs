using System.Text;
using VcrSharp.Core.Rendering;
using VcrSharp.Core.Terminal;

namespace VcrSharp.Infrastructure.Terminal;

/// <summary>
/// The browserless render path: run a command in an in-process pseudoconsole (ConPTY), feed its VT
/// output through <see cref="VtScreen"/>, and snapshot the settled cell grid to a
/// <see cref="TerminalContent"/>. No ttyd, no Chromium. The caller (CLI) renders the snapshot with the
/// existing SvgRenderer, proving the cell grid — not the browser — is all the SVG path ever needed.
/// </summary>
public sealed class NativeTerminalRenderer
{
    private readonly int _cols;
    private readonly int _rows;

    public NativeTerminalRenderer(int cols, int rows)
    {
        _cols = Math.Max(1, cols);
        _rows = Math.Max(1, rows);
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

        // Run the command through PowerShell so shell builtins / pipelines / `dotnet run` all work,
        // then exit (so the PTY reaches EOF). TERM/COLORTERM nudge Spectre to emit 256/truecolor SGR.
        var commandLine = $"pwsh -NoLogo -NoProfile -Command \"{command.Replace("\"", "`\"")}\"";
        var env = new Dictionary<string, string>
        {
            ["TERM"] = "xterm-256color",
            ["COLORTERM"] = "truecolor",
        };

        var pty = ConPtyProcess.Start(commandLine, _cols, _rows, env, workingDirectory);
        var screen = new VtScreen(_cols, _rows);

        // Drain on a background thread: ConPTY only EOFs once the pseudoconsole is closed, so we read
        // until then, wait for the child to exit, then close to flush the tail and finish the drain.
        // Optional raw-stream dump for debugging the ConPTY VT output.
        var dumpPath = Environment.GetEnvironmentVariable("VCR_PTY_DUMP");
        var dump = string.IsNullOrEmpty(dumpPath) ? null : new StringBuilder();

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
                    screen.Feed(text);
                    dump?.Append(text);
                }
            }
        }, cancellationToken);

        pty.WaitForExit(maxMs);
        pty.CloseChild();           // flush tail + signal EOF to the reader
        readTask.Wait(5000);        // bounded: the drain should finish promptly after EOF

        var content = screen.ToTerminalContent();
        pty.Dispose();

        if (dump is not null && dumpPath is not null)
            File.WriteAllText(dumpPath, dump.ToString());

        return Task.FromResult(content);
    }
}
