namespace VcrSharp.Infrastructure.Terminal;

/// <summary>
/// Platform-neutral pseudo-terminal handle: spawns a child attached to a fixed Cols×Rows PTY and
/// exposes its VT output (and an input channel) as <see cref="Stream"/>s. The Windows implementation is
/// <see cref="ConPtyProcess"/> (ConPTY); the Unix implementation is <see cref="UnixPtyProcess"/>
/// (<c>posix_openpt</c> + <c>posix_spawn</c>). The native render path (parser + grid) is platform-neutral
/// — only the PTY plumbing differs, which this seam isolates.
/// </summary>
public interface IPtyProcess : IDisposable
{
    /// <summary>The child's VT output (read this and feed it to a VtScreen).</summary>
    Stream Output { get; }

    /// <summary>The child's stdin (write keystrokes here; the shell echoes them back through Output).</summary>
    Stream Input { get; }

    /// <summary>Resizes the PTY (the child sees a SIGWINCH-equivalent).</summary>
    void Resize(int cols, int rows);

    /// <summary>Blocks until the child exits or the timeout elapses; returns true if it exited.</summary>
    bool WaitForExit(int timeoutMs);

    /// <summary>True once the child process has terminated.</summary>
    bool HasExited { get; }

    /// <summary>
    /// Ends the child cleanly and makes <see cref="Output"/> reach EOF so a draining reader finishes.
    /// Safe to call after the child has already exited (then it is a no-op). Call this — not full
    /// <see cref="IDisposable.Dispose"/> — to let a reader drain to completion before teardown.
    /// </summary>
    void CloseChild();
}

/// <summary>
/// Factory that picks the right <see cref="IPtyProcess"/> for the current OS. Callers supply both a
/// Windows command-line string (for ConPTY's <c>CreateProcess</c>) and a Unix argv vector (for
/// <c>posix_spawn</c>), since the two backends take genuinely different launch shapes — keeping the
/// Windows path byte-for-byte unchanged while admitting the Unix sibling.
/// </summary>
public static class PtyProcess
{
    public static IPtyProcess Start(string windowsCommandLine, IReadOnlyList<string> unixArgv,
        int cols, int rows, IReadOnlyDictionary<string, string>? environment = null, string? workingDirectory = null)
        => OperatingSystem.IsWindows()
            ? ConPtyProcess.Start(windowsCommandLine, cols, rows, environment, workingDirectory)
            : UnixPtyProcess.Start(unixArgv, cols, rows, environment, workingDirectory);
}
