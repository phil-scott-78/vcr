using System.Runtime.InteropServices;

namespace VcrSharp.Infrastructure.Terminal;

/// <summary>
/// The Unix sibling of <see cref="ConPtyProcess"/>: opens a real pseudo-terminal with
/// <c>posix_openpt</c>/<c>grantpt</c>/<c>unlockpt</c>, sizes it to Cols×Rows, and launches the child as a
/// new session leader on the slave side via <c>posix_spawn</c> (with <c>POSIX_SPAWN_SETSID</c> so the PTY
/// becomes the controlling terminal). The master fd is exposed as a bidirectional <see cref="Stream"/>
/// (read = child's VT output, write = child's stdin) — the same role ConPTY plays on Windows, feeding a
/// platform-neutral <c>VtScreen</c>.
/// </summary>
/// <remarks>
/// <c>posix_spawn</c> is used rather than a managed <c>fork</c>/<c>exec</c> on purpose: forking a
/// multi-threaded CLR and then running managed code in the child is unsafe (only the calling thread
/// survives; runtime locks/JIT can deadlock). <c>posix_spawn</c> performs the fork+exec inside libc with
/// no managed code in between, so the session-setup happens via file actions + the SETSID attribute.
/// Works on Linux (glibc) and macOS (libSystem).
/// </remarks>
public sealed class UnixPtyProcess : IPtyProcess
{
    // open(2) flags
    private const int O_RDWR = 0x0002;
    private static int O_NOCTTY => OperatingSystem.IsMacOS() ? 0x20000 : 0x100;

    // ioctl request: TIOCSWINSZ (set window size)
    private static nuint TIOCSWINSZ => OperatingSystem.IsMacOS() ? 0x80087467u : 0x5414u;

    // posix_spawnattr flag: create a new session for the child (it becomes the controlling-tty owner).
    private static short POSIX_SPAWN_SETSID => (short)(OperatingSystem.IsMacOS() ? 0x0400 : 0x0080);

    private const int WNOHANG = 1;
    private const int SIGHUP = 1;
    private const int SIGKILL = 9;
    private const int EINTR = 4;
    private const int EIO = 5;

    private int _master = -1;
    private int _pid = -1;
    private bool _reaped;
    private PtyStream? _stream;
    private bool _disposed;

    public Stream Output { get; private set; } = Stream.Null;
    public Stream Input { get; private set; } = Stream.Null;

    /// <summary>
    /// Starts <paramref name="argv"/> (argv[0] is the executable; resolved against
    /// <paramref name="environment"/>'s PATH if it has no slash) in a fresh PTY of size
    /// <paramref name="cols"/>×<paramref name="rows"/>. <paramref name="environment"/> is layered on top of
    /// the current process environment.
    /// </summary>
    public static UnixPtyProcess Start(IReadOnlyList<string> argv, int cols, int rows,
        IReadOnlyDictionary<string, string>? environment = null, string? workingDirectory = null)
    {
        if (OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("UnixPtyProcess requires a Unix-like OS.");
        if (argv is null || argv.Count == 0)
            throw new ArgumentException("argv must contain at least the executable.", nameof(argv));

        var p = new UnixPtyProcess();
        p.StartCore(argv, cols, rows, environment, workingDirectory);
        return p;
    }

    private void StartCore(IReadOnlyList<string> argv, int cols, int rows,
        IReadOnlyDictionary<string, string>? environment, string? workingDirectory)
    {
        // 1. Open + unlock the master, then learn the slave device path.
        _master = posix_openpt(O_RDWR | O_NOCTTY);
        if (_master < 0) throw Fail("posix_openpt");
        if (grantpt(_master) != 0) throw Fail("grantpt");
        if (unlockpt(_master) != 0) throw Fail("unlockpt");
        var slavePath = GetSlaveName(_master);

        // 2. Size the PTY before the child starts so it sees the right geometry from byte one.
        SetWinsize(cols, rows);

        // 3. Build the merged environment + resolve the executable to an absolute path (posix_spawn does
        //    no PATH search; posix_spawnp would search the *caller's* PATH, not our overridden one).
        var env = BuildEnvironment(environment);
        var exe = ResolveExecutable(argv[0], env);

        // 4. Marshal argv/envp/exe/slavePath to native UTF-8 (freed in the finally below).
        var toFree = new List<IntPtr>();
        var faBuf = Marshal.AllocHGlobal(1024);
        var attrBuf = Marshal.AllocHGlobal(1024);
        Zero(faBuf, 1024);
        Zero(attrBuf, 1024);

        try
        {
            var argvPtr = MarshalStringArray(argv, toFree);
            var envpPtr = MarshalStringArray(env, toFree);
            var exePtr = Utf8(exe, toFree);
            var slavePtr = Utf8(slavePath, toFree);

            if (posix_spawn_file_actions_init(faBuf) != 0) throw Fail("posix_spawn_file_actions_init");
            if (posix_spawnattr_init(attrBuf) != 0) throw Fail("posix_spawnattr_init");

            // New session => the first tty opened (the slave, without O_NOCTTY) becomes the controlling
            // terminal, so job control / TUIs behave as on a real terminal.
            posix_spawnattr_setflags(attrBuf, POSIX_SPAWN_SETSID);

            // Optional cwd (best-effort: addchdir_np is glibc 2.29+/macOS 10.15+; ignore if unavailable).
            if (!string.IsNullOrEmpty(workingDirectory))
                TryAddChdir(faBuf, workingDirectory, toFree);

            // Wire the slave to the child's stdio, then drop the inherited master fd.
            posix_spawn_file_actions_addopen(faBuf, 0, slavePtr, O_RDWR, 0);
            posix_spawn_file_actions_adddup2(faBuf, 0, 1);
            posix_spawn_file_actions_adddup2(faBuf, 0, 2);
            posix_spawn_file_actions_addclose(faBuf, _master);

            var rc = posix_spawn(out _pid, exePtr, faBuf, attrBuf, argvPtr, envpPtr);
            if (rc != 0)
                throw new IOException($"posix_spawn('{exe}') failed: {rc} ({Errno(rc)})");
        }
        finally
        {
            posix_spawn_file_actions_destroy(faBuf);
            posix_spawnattr_destroy(attrBuf);
            Marshal.FreeHGlobal(faBuf);
            Marshal.FreeHGlobal(attrBuf);
            foreach (var p in toFree) Marshal.FreeCoTaskMem(p);
        }

        _stream = new PtyStream(_master);
        Output = _stream;
        Input = _stream;
    }

    public void Resize(int cols, int rows)
    {
        if (_master >= 0) SetWinsize(cols, rows);
    }

    private void SetWinsize(int cols, int rows)
    {
        var ws = new Winsize { ws_row = (ushort)Math.Max(1, rows), ws_col = (ushort)Math.Max(1, cols) };
        ioctl(_master, TIOCSWINSZ, ref ws);
    }

    public bool HasExited
    {
        get
        {
            if (_reaped) return true;
            if (_pid <= 0) return true;
            var r = waitpid(_pid, out _, WNOHANG);
            if (r == _pid) { _reaped = true; return true; }
            return false; // r == 0: still running (r < 0: already reaped/errored — treat as not-yet here)
        }
    }

    public bool WaitForExit(int timeoutMs)
    {
        var deadline = Environment.TickCount64 + Math.Max(0, timeoutMs);
        while (!HasExited)
        {
            if (Environment.TickCount64 >= deadline) return false;
            Thread.Sleep(10);
        }
        return true;
    }

    /// <summary>
    /// On Unix the read fd <em>is</em> the master, so there is no separate handle to close for EOF: once
    /// the child exits, the kernel closes the slave and a blocked master read returns EIO (mapped to EOF).
    /// So if the child already exited this is a no-op; if it is still running (e.g. a timeout), send SIGHUP
    /// so it terminates and the reader drains to EOF.
    /// </summary>
    public void CloseChild()
    {
        if (_pid > 0 && !HasExited)
            kill(_pid, SIGHUP);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // SIGKILL anything still alive, then reap to avoid a zombie.
        if (_pid > 0 && !HasExited)
        {
            kill(_pid, SIGKILL);
            waitpid(_pid, out _, 0);
            _reaped = true;
        }

        try { _stream?.Dispose(); } catch { /* ignore */ }
        _master = -1;
    }

    // ---- helpers ----

    private static string GetSlaveName(int master)
    {
        if (OperatingSystem.IsMacOS())
        {
            var p = ptsname(master); // not thread-safe, but we are single-threaded here
            if (p == IntPtr.Zero) throw Fail("ptsname");
            return Marshal.PtrToStringUTF8(p) ?? throw new IOException("ptsname returned no name");
        }

        var buf = new byte[256];
        var rc = ptsname_r(master, buf, (nuint)buf.Length);
        if (rc != 0) throw new IOException($"ptsname_r failed: {Errno(rc)}");
        var len = Array.IndexOf(buf, (byte)0);
        return System.Text.Encoding.UTF8.GetString(buf, 0, len < 0 ? buf.Length : len);
    }

    private static Dictionary<string, string> BuildEnvironment(IReadOnlyDictionary<string, string>? overrides)
    {
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (System.Collections.DictionaryEntry e in Environment.GetEnvironmentVariables())
            merged[(string)e.Key] = (string?)e.Value ?? "";
        if (overrides != null)
            foreach (var (k, v) in overrides) merged[k] = v;
        return merged;
    }

    private static string ResolveExecutable(string name, IReadOnlyDictionary<string, string> env)
    {
        if (name.Contains('/')) return name;
        var path = env.TryGetValue("PATH", out var p) && !string.IsNullOrEmpty(p)
            ? p
            : Environment.GetEnvironmentVariable("PATH") ?? "/usr/bin:/bin";
        foreach (var dir in path.Split(':', StringSplitOptions.RemoveEmptyEntries))
        {
            var full = Path.Combine(dir, name);
            if (File.Exists(full)) return full;
        }
        return name; // let posix_spawn surface the failure
    }

    private static void TryAddChdir(IntPtr faBuf, string dir, List<IntPtr> toFree)
    {
        try { posix_spawn_file_actions_addchdir_np(faBuf, Utf8(dir, toFree)); }
        catch (EntryPointNotFoundException) { /* older libc: run in the inherited cwd */ }
    }

    private static IntPtr MarshalStringArray(IReadOnlyList<string> items, List<IntPtr> toFree)
    {
        var ptrs = new IntPtr[items.Count + 1];
        for (var i = 0; i < items.Count; i++)
            ptrs[i] = Utf8(items[i], toFree);
        ptrs[^1] = IntPtr.Zero;
        var arr = Marshal.AllocCoTaskMem(IntPtr.Size * ptrs.Length);
        toFree.Add(arr);
        Marshal.Copy(ptrs, 0, arr, ptrs.Length);
        return arr;
    }

    private static IntPtr MarshalStringArray(Dictionary<string, string> env, List<IntPtr> toFree)
        => MarshalStringArray(env.Select(kv => $"{kv.Key}={kv.Value}").ToList(), toFree);

    private static IntPtr Utf8(string s, List<IntPtr> toFree)
    {
        var p = Marshal.StringToCoTaskMemUTF8(s);
        toFree.Add(p);
        return p;
    }

    private static void Zero(IntPtr p, int len)
    {
        var zeros = new byte[len];
        Marshal.Copy(zeros, 0, p, len);
    }

    private static IOException Fail(string call)
        => new($"{call} failed: {Errno(Marshal.GetLastPInvokeError())}");

    private static string Errno(int e) => $"errno {e}";

    [StructLayout(LayoutKind.Sequential)]
    private struct Winsize
    {
        public ushort ws_row, ws_col, ws_xpixel, ws_ypixel;
    }

    /// <summary>A duplex <see cref="Stream"/> over the PTY master fd. Reads map EIO to a clean EOF (the
    /// slave closing on child exit), so a drain loop terminates instead of throwing.</summary>
    private sealed class PtyStream(int fd) : Stream
    {
        private bool _closed;

        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_closed) return 0;
            var h = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                var p = h.AddrOfPinnedObject() + offset;
                while (true)
                {
                    var n = read(fd, p, (nuint)count);
                    if (n >= 0) return (int)n;
                    var e = Marshal.GetLastPInvokeError();
                    if (e == EINTR) continue;
                    if (e == EIO) return 0; // slave closed → EOF
                    throw new IOException($"pty read failed (errno {e})");
                }
            }
            finally { h.Free(); }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var h = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                var basePtr = h.AddrOfPinnedObject() + offset;
                var written = 0;
                while (written < count)
                {
                    var n = write(fd, basePtr + written, (nuint)(count - written));
                    if (n < 0)
                    {
                        var e = Marshal.GetLastPInvokeError();
                        if (e == EINTR) continue;
                        throw new IOException($"pty write failed (errno {e})");
                    }
                    written += (int)n;
                }
            }
            finally { h.Free(); }
        }

        protected override void Dispose(bool disposing)
        {
            if (!_closed && fd >= 0) close(fd);
            _closed = true;
            base.Dispose(disposing);
        }
    }

    // ---- P/Invoke (libc / libSystem) ----

    [DllImport("libc", SetLastError = true)] private static extern int posix_openpt(int flags);
    [DllImport("libc", SetLastError = true)] private static extern int grantpt(int fd);
    [DllImport("libc", SetLastError = true)] private static extern int unlockpt(int fd);
    [DllImport("libc", SetLastError = true)] private static extern int ptsname_r(int fd, byte[] buf, nuint buflen);
    [DllImport("libc", SetLastError = true)] private static extern IntPtr ptsname(int fd);

    [DllImport("libc", SetLastError = true)] private static extern int ioctl(int fd, nuint request, ref Winsize ws);

    [DllImport("libc", SetLastError = true)] private static extern nint read(int fd, IntPtr buf, nuint count);
    [DllImport("libc", SetLastError = true)] private static extern nint write(int fd, IntPtr buf, nuint count);
    [DllImport("libc", SetLastError = true)] private static extern int close(int fd);

    [DllImport("libc", SetLastError = true)] private static extern int kill(int pid, int sig);
    [DllImport("libc", SetLastError = true)] private static extern int waitpid(int pid, out int status, int options);

    [DllImport("libc", SetLastError = true)]
    private static extern int posix_spawn(out int pid, IntPtr path, IntPtr fileActions, IntPtr attrp, IntPtr argv, IntPtr envp);

    [DllImport("libc")] private static extern int posix_spawn_file_actions_init(IntPtr fa);
    [DllImport("libc")] private static extern int posix_spawn_file_actions_destroy(IntPtr fa);
    [DllImport("libc")] private static extern int posix_spawn_file_actions_addopen(IntPtr fa, int fd, IntPtr path, int oflag, uint mode);
    [DllImport("libc")] private static extern int posix_spawn_file_actions_adddup2(IntPtr fa, int fd, int newfd);
    [DllImport("libc")] private static extern int posix_spawn_file_actions_addclose(IntPtr fa, int fd);
    [DllImport("libc")] private static extern int posix_spawn_file_actions_addchdir_np(IntPtr fa, IntPtr path);

    [DllImport("libc")] private static extern int posix_spawnattr_init(IntPtr attr);
    [DllImport("libc")] private static extern int posix_spawnattr_destroy(IntPtr attr);
    [DllImport("libc")] private static extern int posix_spawnattr_setflags(IntPtr attr, short flags);
}
