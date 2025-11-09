using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace VcrSharp.Infrastructure.Processes;

/// <summary>
/// Manages the ttyd process lifecycle.
/// </summary>
public class TtydProcess : IDisposable
{
    private Process? _process;
    private readonly List<string> _shellCommand;
    private readonly List<string> _execCommands;
    private readonly string? _workingDirectory;
    private readonly Dictionary<string, string> _environmentVariables;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TtydProcess"/> class.
    /// </summary>
    /// <param name="shellCommand">The complete shell command including executable and all arguments.
    /// First element is the shell executable, remaining elements are its arguments.</param>
    /// <param name="execCommands">Optional list of commands to execute at startup (for Exec command support).</param>
    /// <param name="workingDirectory">Optional working directory for the terminal session.
    /// If not specified, defaults to the current directory.</param>
    /// <param name="environmentVariables">Optional environment variables to set for the shell process.
    /// These are merged with system environment variables.</param>
    public TtydProcess(
        List<string> shellCommand,
        List<string>? execCommands = null,
        string? workingDirectory = null,
        Dictionary<string, string>? environmentVariables = null)
    {
        if (shellCommand == null || shellCommand.Count == 0)
            throw new ArgumentException("Shell command cannot be null or empty", nameof(shellCommand));

        _shellCommand = shellCommand;
        _execCommands = execCommands ?? [];
        _workingDirectory = workingDirectory;
        _environmentVariables = environmentVariables ?? new Dictionary<string, string>();
    }

    /// <summary>
    /// Gets the port ttyd is listening on.
    /// </summary>
    public int Port { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the process is running.
    /// </summary>
    public bool IsRunning => _process is { HasExited: false };

    /// <summary>
    /// Starts the ttyd process.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task StartAsync()
    {
        if (IsRunning)
            throw new InvalidOperationException("ttyd process is already running");

        // Find an available port (let OS assign)
        Port = GetRandomPort();

        // Build the command line for ttyd
        // ttyd --port=<port> --interface 127.0.0.1 -w <workdir> -t options... --writable <shell> <args...>
        var workingDir = _workingDirectory ?? Environment.CurrentDirectory;
        var args = new List<string>
        {
            $"--port={Port}",
            "--interface", "127.0.0.1",
            "-w", workingDir,
            "-t", "rendererType=canvas",
            "-t", "disableResizeOverlay=true",
            "-t", "enableSixel=true",
            "-t", "customGlyphs=true",
            "--writable"  // CRITICAL: Enable keyboard input
        };

        // If we have exec commands, modify the shell command to run them first
        if (_execCommands.Count > 0)
        {
            // Build a script that executes all commands then starts an interactive shell
            // Format: bash -c "sleep 1 && cmd1; cmd2; cmd3; exec bash"
            // The 'exec bash' replaces the script process with an interactive shell
            var script = BuildStartupScript();
            args.Add(_shellCommand[0]); // Shell executable (e.g., "bash", "pwsh")
            args.Add("-c");
            args.Add(script);
        }
        else
        {
            // No exec commands, just use the shell as-is
            args.AddRange(_shellCommand);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "ttyd",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Add arguments
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        // Set environment variables
        // Important: We need to explicitly set the environment, not inherit it automatically
        // First, add custom environment variables (shell-specific + user-defined)
        foreach (var (key, value) in _environmentVariables)
        {
            startInfo.EnvironmentVariables[key] = value;
        }

        // Then, copy all system environment variables (custom vars take precedence due to dictionary behavior)
        foreach (System.Collections.DictionaryEntry envVar in Environment.GetEnvironmentVariables())
        {
            var key = envVar.Key?.ToString();
            var value = envVar.Value?.ToString();
            if (key != null && value != null && !startInfo.EnvironmentVariables.ContainsKey(key))
            {
                startInfo.EnvironmentVariables[key] = value;
            }
        }

        _process = Process.Start(startInfo);

        if (_process == null)
            throw new InvalidOperationException("Failed to start ttyd process");

        // Start async tasks to read stdout/stderr to prevent blocking
        _ = Task.Run(() => ReadProcessOutputAsync(_process.StandardOutput));
        _ = Task.Run(() => ReadProcessOutputAsync(_process.StandardError));

        // Wait for ttyd to be ready
        await WaitForReadyAsync();
    }

    /// <summary>
    /// Reads process output stream asynchronously to prevent blocking.
    /// </summary>
    private static async Task ReadProcessOutputAsync(StreamReader reader)
    {
        try
        {
            while (await reader.ReadLineAsync() is not null)
            {
                // Silently consume output to prevent blocking
            }
        }
        catch
        {
            // Ignore errors from output reading
        }
    }

    /// <summary>
    /// Builds a startup script that runs exec commands.
    /// </summary>
    private string BuildStartupScript()
    {
        // Escape single quotes in commands for shell safety
        var escapedCommands = _execCommands
            .Select(cmd => cmd.Replace("'", "'\\''"))
            .ToList();

        // Build script: sleep briefly to let browser connect, then run commands (separated by ;)
        // Using ; instead of && so that failed commands don't stop the script
        // Commands run in background while recording proceeds immediately
        var commandChain = string.Join("; ", escapedCommands);

        return $"sleep 1.5; {commandChain}";
    }

    /// <summary>
    /// Gets a random available port in the ephemeral port range.
    /// </summary>
    private static int GetRandomPort()
    {
        // Let the OS assign a random available port by binding to port 0
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>
    /// Waits for ttyd to be ready by checking if the port is accepting connections.
    /// </summary>
    private async Task WaitForReadyAsync()
    {
        const int MaxAttempts = 50; // 5 seconds total (50 * 100ms)
        const int DelayMs = 100;

        for (var i = 0; i < MaxAttempts; i++)
        {
            // Check if process has exited (indicates startup failure)
            if (_process?.HasExited == true)
            {
                var exitCode = _process.ExitCode;
                throw new InvalidOperationException($"ttyd process exited unexpectedly with code {exitCode}");
            }

            // Try to connect to the port
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, Port);
                // Connection successful - ttyd is ready
                return;
            }
            catch (SocketException)
            {
                // Port not ready yet, continue waiting
            }

            await Task.Delay(DelayMs);
        }

        throw new TimeoutException($"ttyd did not become ready on port {Port} within the timeout period.");
    }

    /// <summary>
    /// Stops the ttyd process.
    /// </summary>
    private void Stop()
    {
        if (_process is { HasExited: false })
        {
            ProcessHelper.KillProcessTree(_process);
            _process.WaitForExit(5000);
            _process.Dispose();
            _process = null;
        }
    }

    /// <summary>
    /// Disposes the ttyd process.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}