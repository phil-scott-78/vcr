using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace VcrSharp.Infrastructure.Terminal;

/// <summary>
/// Spawns a child process attached to a Windows pseudoconsole (ConPTY) of a fixed Cols×Rows size and
/// exposes its VT output as a <see cref="Stream"/> — no ttyd, no browser. ConPTY hosts the child in a
/// real pseudo-terminal and re-emits a VT stream sized to the console, which we feed to a VtScreen.
/// Windows 10 1809+ only (the browserless path is Windows-first for now).
/// </summary>
public sealed class ConPtyProcess : IPtyProcess
{
    private const int STARTF_USESTDHANDLES = 0x00000100;
    private const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
    private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
    private static readonly IntPtr PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;

    private IntPtr _pseudoConsole = IntPtr.Zero;
    private IntPtr _attributeList = IntPtr.Zero;
    private SafeFileHandle? _inputWrite;   // we write -> child stdin
    private SafeFileHandle? _outputRead;   // we read  <- child stdout (VT stream)
    private PROCESS_INFORMATION _processInfo;
    private bool _disposed;

    public Stream Output { get; private set; } = Stream.Null;
    public Stream Input { get; private set; } = Stream.Null;

    /// <summary>
    /// Starts <paramref name="commandLine"/> in a fresh pseudoconsole sized <paramref name="cols"/>×
    /// <paramref name="rows"/>. <paramref name="environment"/> is layered on top of the current process
    /// environment (set TERM/COLORTERM here so the child emits rich ANSI).
    /// </summary>
    public static ConPtyProcess Start(string commandLine, int cols, int rows,
        IReadOnlyDictionary<string, string>? environment = null, string? workingDirectory = null)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("ConPtyProcess requires Windows (ConPTY).");

        var p = new ConPtyProcess();
        // If anything after allocating the pseudoconsole / attribute list throws (e.g. CreateProcess
        // fails), Dispose() frees them — otherwise the HPC handle + HGLOBAL attribute list would leak.
        try { p.StartCore(commandLine, cols, rows, environment, workingDirectory); }
        catch { p.Dispose(); throw; }
        return p;
    }

    private void StartCore(string commandLine, int cols, int rows,
        IReadOnlyDictionary<string, string>? environment, string? workingDirectory)
    {
        // Two pipes: input (we -> child) and output (child -> we).
        if (!CreatePipe(out var inputRead, out var inputWrite, IntPtr.Zero, 0))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreatePipe(input) failed");
        if (!CreatePipe(out var outputRead, out var outputWrite, IntPtr.Zero, 0))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreatePipe(output) failed");

        _inputWrite = inputWrite;
        _outputRead = outputRead;

        // Create the pseudoconsole bound to the child-side pipe ends.
        var size = new COORD { X = (short)cols, Y = (short)rows };
        var hr = CreatePseudoConsole(size, inputRead, outputWrite, 0, out _pseudoConsole);
        if (hr != 0)
            throw new Win32Exception(hr, "CreatePseudoConsole failed");

        // ConPTY duplicates the child-side handles; close our copies so EOF propagates correctly.
        inputRead.Dispose();
        outputWrite.Dispose();

        // STARTUPINFOEX carrying the PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE attribute.
        var startupInfo = BuildStartupInfoEx();

        var envBlock = BuildEnvironmentBlock(environment);
        var flags = EXTENDED_STARTUPINFO_PRESENT | (envBlock != null ? CREATE_UNICODE_ENVIRONMENT : 0);

        var cmd = new StringBuilder(commandLine);
        var success = CreateProcess(
            null, cmd, IntPtr.Zero, IntPtr.Zero, false, flags,
            envBlock, workingDirectory, ref startupInfo, out _processInfo);

        if (!success)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateProcess (ConPTY child) failed");

        Output = new FileStream(_outputRead, FileAccess.Read, 4096, isAsync: false);
        Input = new FileStream(_inputWrite, FileAccess.Write, 4096, isAsync: false);
    }

    private STARTUPINFOEX BuildStartupInfoEx()
    {
        var startupInfo = new STARTUPINFOEX();
        startupInfo.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>();

        // Size the attribute list, allocate, init, then attach the pseudoconsole.
        var listSize = IntPtr.Zero;
        InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref listSize);
        _attributeList = Marshal.AllocHGlobal(listSize);
        if (!InitializeProcThreadAttributeList(_attributeList, 1, 0, ref listSize))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "InitializeProcThreadAttributeList failed");

        if (!UpdateProcThreadAttribute(_attributeList, 0, PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                _pseudoConsole, IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "UpdateProcThreadAttribute failed");

        startupInfo.lpAttributeList = _attributeList;
        startupInfo.StartupInfo.dwFlags = STARTF_USESTDHANDLES;
        return startupInfo;
    }

    private static char[]? BuildEnvironmentBlock(IReadOnlyDictionary<string, string>? overrides)
    {
        if (overrides == null || overrides.Count == 0)
            return null;

        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Collections.DictionaryEntry e in Environment.GetEnvironmentVariables())
            merged[(string)e.Key] = (string?)e.Value ?? "";
        foreach (var (k, v) in overrides)
            merged[k] = v;

        var sb = new StringBuilder();
        foreach (var (k, v) in merged)
            sb.Append(k).Append('=').Append(v).Append('\0');
        sb.Append('\0');
        return sb.ToString().ToCharArray();
    }

    /// <summary>Resizes the pseudoconsole (the child sees a SIGWINCH-equivalent).</summary>
    public void Resize(int cols, int rows)
    {
        if (_pseudoConsole != IntPtr.Zero)
            ResizePseudoConsole(_pseudoConsole, new COORD { X = (short)cols, Y = (short)rows });
    }

    /// <summary>Blocks until the child exits or the timeout elapses; returns true if it exited.</summary>
    public bool WaitForExit(int timeoutMs)
        => _processInfo.hProcess != IntPtr.Zero && WaitForSingleObject(_processInfo.hProcess, (uint)timeoutMs) == 0;

    // Liveness from the wait state, NOT the exit code: a process that legitimately exits with code 259
    // would otherwise be reported as forever-running (STILL_ACTIVE collision). WAIT_OBJECT_0 (0) = signaled.
    public bool HasExited =>
        _processInfo.hProcess == IntPtr.Zero ||
        WaitForSingleObject(_processInfo.hProcess, 0) == 0;

    /// <summary>
    /// Closes the pseudoconsole, which flushes the child's remaining output and makes the output read
    /// end reach EOF. Call this (not full <see cref="Dispose"/>) to let a reader drain to completion
    /// without tearing down the streams it is still reading.
    /// </summary>
    public void CloseChild()
    {
        if (_pseudoConsole != IntPtr.Zero)
        {
            ClosePseudoConsole(_pseudoConsole);
            _pseudoConsole = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        CloseChild();

        // ConPTY does not kill the attached child when its pseudoconsole closes — a runaway or
        // close-ignoring child would be orphaned (and CloseHandle below only drops our handle reference,
        // it does not stop the process). Give it a brief grace to exit on its own, then force-terminate,
        // mirroring the Unix backend's SIGKILL+reap so behavior matches across platforms.
        if (_processInfo.hProcess != IntPtr.Zero && !WaitForExit(200))
        {
            try { TerminateProcess(_processInfo.hProcess, 1); } catch { /* already gone */ }
        }

        try { Input.Dispose(); } catch { /* ignore */ }
        try { Output.Dispose(); } catch { /* ignore */ }

        if (_attributeList != IntPtr.Zero)
        {
            DeleteProcThreadAttributeList(_attributeList);
            Marshal.FreeHGlobal(_attributeList);
            _attributeList = IntPtr.Zero;
        }

        if (_processInfo.hThread != IntPtr.Zero) CloseHandle(_processInfo.hThread);
        if (_processInfo.hProcess != IntPtr.Zero) CloseHandle(_processInfo.hProcess);
        _processInfo = default;
    }

    // ---- P/Invoke ----

    [StructLayout(LayoutKind.Sequential)]
    private struct COORD { public short X; public short Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct STARTUPINFO
    {
        public int cb;
        public IntPtr lpReserved, lpDesktop, lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct STARTUPINFOEX
    {
        public STARTUPINFO StartupInfo;
        public IntPtr lpAttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess, hThread;
        public int dwProcessId, dwThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CreatePipe(out SafeFileHandle hReadPipe, out SafeFileHandle hWritePipe, IntPtr lpPipeAttributes, int nSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int CreatePseudoConsole(COORD size, SafeFileHandle hInput, SafeFileHandle hOutput, uint dwFlags, out IntPtr phPC);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int ResizePseudoConsole(IntPtr hPC, COORD size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int ClosePseudoConsole(IntPtr hPC);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool InitializeProcThreadAttributeList(IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UpdateProcThreadAttribute(IntPtr lpAttributeList, uint dwFlags, IntPtr attribute, IntPtr lpValue, IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcess(
        string? lpApplicationName, StringBuilder lpCommandLine, IntPtr lpProcessAttributes, IntPtr lpThreadAttributes,
        bool bInheritHandles, uint dwCreationFlags, char[]? lpEnvironment, string? lpCurrentDirectory,
        ref STARTUPINFOEX lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
}
