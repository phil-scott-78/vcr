using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using VcrSharp.Core.Rendering;
using VcrSharp.Core.Session;
using VcrSharp.Terminal;

namespace VcrSharp.Infrastructure.Terminal;

/// <summary>
/// <see cref="ITerminalPage"/> backed by an in-process PTY (ConPTY/Unix) + <see cref="VtScreen"/>.
/// Tape commands (Type/Key/Modifier/Wait/Hide/Show) run against this surface: input is written to the
/// pseudo-terminal's stdin and the real shell echoes it back through the parser; reads come from the live
/// cell grid. <paramref name="gate"/> is the lock shared with the drain thread so a snapshot never tears
/// against an in-flight <c>Feed</c>.
/// </summary>
public sealed class NativeTerminalPage(IPtyProcess pty, VtScreen screen, Lock gate, SessionOptions options)
    : ITerminalPage
{
    private string _clipboard = string.Empty;
    private bool _cursorHidden;

    // A small pause after each key so a TUI redraws (and the drain captures the new frame) before the
    // next key — the browser gets this for free from input-pipeline latency; native is instant, so rapid
    // key bursts (e.g. four Downs with no Sleep) would otherwise collapse into one captured frame.
    private const int KeyPaceMs = 24;

    public Task TypeAsync(string text, int delayMs) => TypeAsync(text, delayMs, CancellationToken.None);

    private async Task TypeAsync(string text, int delayMs, CancellationToken cancellationToken)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            Write(rune.ToString());
            if (delayMs > 0) await Task.Delay(delayMs, cancellationToken);
        }
    }

    public async Task PressKeyAsync(string key)
    {
        Write(NativeKeyMap.ForKey(key));
        await Task.Delay(KeyPaceMs);
    }

    public async Task PressKeyCombinationAsync(List<string> modifiers, string key, CancellationToken cancellationToken)
    {
        Write(NativeKeyMap.ForCombination(modifiers, key));
        await Task.Delay(KeyPaceMs, cancellationToken);
    }

    public async Task<bool> WaitForPatternAsync(Regex pattern, string scope, int timeoutMs, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (pattern.IsMatch(ScreenText())) return true;
            if (pty.HasExited) { await SettleAfterExitAsync(cancellationToken); return true; }
            await Task.Delay(25, cancellationToken);
        }
        return pattern.IsMatch(ScreenText());
    }

    public async Task<(bool Matched, string UpdatedPersistentBuffer, string UpdatedLastSnapshot)>
        WaitForPatternInPersistentBufferAsync(Regex pattern, string persistentBuffer, string lastSnapshot,
            int timeout = 15000, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var text = ScreenText();
        while (sw.ElapsedMilliseconds < timeout)
        {
            text = ScreenText();
            if (pattern.IsMatch(text)) return (true, text, text);
            if (pty.HasExited)
            {
                await SettleAfterExitAsync(cancellationToken);
                text = ScreenText();
                return (true, text, text);
            }
            await Task.Delay(25, cancellationToken);
        }
        return (pattern.IsMatch(text), text, text);
    }

    /// <summary>
    /// A bare <c>Wait</c> defaults to the shell-prompt pattern — fine for an interactive session, but a
    /// non-interactive <c>Exec</c> shell <em>exits</em> instead of re-prompting, so that prompt never
    /// comes. Rather than block to the timeout and fail the recording, once the child has exited we treat
    /// the wait as satisfied (the command is genuinely done): briefly let the drain flush the command's
    /// tail so the next <c>Screenshot</c>/frame sees the full output, then return. Inert for interactive
    /// tapes, where the shell stays alive and prompt-matching works as before. Same on Windows and Unix.
    /// </summary>
    private async Task SettleAfterExitAsync(CancellationToken cancellationToken)
    {
        var last = ScreenText();
        var stableSince = Stopwatch.StartNew();
        var overall = Stopwatch.StartNew();
        while (overall.ElapsedMilliseconds < 1000)
        {
            await Task.Delay(20, cancellationToken);
            var now = ScreenText();
            if (now != last) { last = now; stableSince.Restart(); }
            else if (stableSince.ElapsedMilliseconds >= 150) return;
        }
    }

    public Task CopyToClipboardAsync(string text, CancellationToken cancellationToken)
    {
        _clipboard = text;
        return Task.CompletedTask;
    }

    public Task PasteFromClipboardAsync(CancellationToken cancellationToken)
    {
        Write(_clipboard);
        return Task.CompletedTask;
    }

    public Task<TerminalContent> GetTerminalContentWithStylesAsync() => Task.FromResult(Snapshot());

    public Task HideCursorAsync()
    {
        _cursorHidden = true;
        return Task.CompletedTask;
    }

    public async Task WaitForBufferContentAsync(int timeout = 1000)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeout)
        {
            if (ScreenText().Length > 0) return;
            await Task.Delay(20);
        }
    }

    /// <summary>Snapshot honoring the hidden-cursor / DisableCursor state.</summary>
    public TerminalContent Snapshot()
    {
        TerminalContent content;
        lock (gate) content = screen.ToTerminalContent();
        if (_cursorHidden || options.DisableCursor) content.CursorVisible = false;
        return content;
    }

    private string ScreenText()
    {
        var content = Snapshot();
        var sb = new StringBuilder();
        foreach (var row in content.Cells)
        {
            var line = new StringBuilder();
            foreach (var cell in row)
                if (cell.Character.Length > 0) line.Append(cell.Character);
            sb.Append(line.ToString().TrimEnd()).Append('\n');
        }
        return sb.ToString();
    }

    private void Write(string s)
    {
        if (string.IsNullOrEmpty(s)) return;
        var bytes = Encoding.UTF8.GetBytes(s);
        pty.Input.Write(bytes, 0, bytes.Length);
        pty.Input.Flush();
    }
}
