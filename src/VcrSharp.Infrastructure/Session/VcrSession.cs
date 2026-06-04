using System.Diagnostics;
using VcrSharp.Core.Logging;
using VcrSharp.Core.Parsing.Ast;
using VcrSharp.Core.Recording;
using VcrSharp.Core.Rendering;
using VcrSharp.Core.Session;
using VcrSharp.Infrastructure.Playwright;
using VcrSharp.Infrastructure.Processes;
using VcrSharp.Infrastructure.Recording;
using VcrSharp.Infrastructure.Rendering;

namespace VcrSharp.Infrastructure.Session;

/// <summary>
/// Main orchestrator for VcrSharp recording sessions.
/// Manages ttyd, browser, terminal, frame capture, and command execution.
/// </summary>
public class VcrSession : IAsyncDisposable
{
    private readonly SessionOptions _options;
    private readonly SessionState _state = new();

    public VcrSession(SessionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }
    private bool _disposed;
    private bool _framesCleanedUp;

    // Resources managed by this session
    private TtydProcess? _ttydProcess;
    private PlaywrightBrowser? _browser;
    private TerminalPage? _terminalPage;
    private FrameStorage? _frameStorage;
    private FrameCapture? _frameCapture;
    private ActivityMonitor? _activityMonitor;

    /// <summary>
    /// Records a tape file and produces output videos.
    /// </summary>
    /// <param name="commands">List of parsed commands to execute</param>
    /// <param name="progress">Optional progress reporter for status updates</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Recording result with output file paths</returns>
    public async Task<RecordingResult> RecordAsync(List<ICommand> commands,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            progress?.Report("Initializing recording session...");

            // 1. Collect Exec commands to run at startup
            var execCommands = commands.OfType<ExecCommand>().ToList();

            // 2. Start ttyd process with properly configured shell
            var shellConfig = ShellConfiguration.GetConfiguration(_options.Shell);
            var shellCommand = shellConfig.BuildTtydCommand();

            // Merge environment variables: shell-specific env vars + custom env vars from tape file
            var environmentVariables = new Dictionary<string, string>(shellConfig.Environment);
            foreach (var (key, value) in _options.Environment)
            {
                environmentVariables[key] = value; // Custom env vars override shell defaults
            }

            // Set COLUMNS and LINES if Cols/Rows are specified
            if (_options.Cols.HasValue)
            {
                environmentVariables["COLUMNS"] = _options.Cols.Value.ToString();
            }
            if (_options.Rows.HasValue)
            {
                environmentVariables["LINES"] = _options.Rows.Value.ToString();
            }

            _ttydProcess = new TtydProcess(
                shellCommand,
                shellConfig,
                execCommands.Select(cmd => cmd.Command).ToList(),
                _options.WorkingDirectory,
                environmentVariables,
                _options.StartupDelay);
            await _ttydProcess.StartAsync();

            // 3. Launch Playwright browser
            _browser = new PlaywrightBrowser();
            await _browser.LaunchAsync(headless: true);

            // 4. Create page and navigate to ttyd
            var url = $"http://localhost:{_ttydProcess.Port}";
            var page = await _browser.NewPageAsync(url, _options.Width, _options.Height, _options.Padding, _options.Cols, _options.Rows, _options.FontSize);
            _terminalPage = new TerminalPage(page);

            // 5. Wait for terminal initialization
            await _terminalPage.WaitForTerminalReadyAsync();

            // 6. Configure terminal with theme and options FIRST (so fonts are loaded before measuring)
            await ConfigureTerminalAsync();

            // 7. If Cols/Rows specified, resize terminal AFTER font options are set
            // (this ensures cell dimensions are measured with the correct font)
            if (_options.Cols.HasValue || _options.Rows.HasValue)
            {
                var (viewportWidth, viewportHeight, cellWidth, cellHeight) = await _terminalPage.ResizeTerminalToColsRowsAsync(_options.Cols, _options.Rows);

                // Store actual measured cell dimensions for SVG encoder to use
                _options.ActualCellWidth = cellWidth;
                _options.ActualCellHeight = cellHeight;

                // Update Width/Height in options so VideoEncoder uses the correct dimensions
                // Add padding back since VideoEncoder expects Width/Height to include padding
                _options.Width = viewportWidth + 2 * _options.Padding;
                _options.Height = viewportHeight + 2 * _options.Padding;

                VcrLogger.Logger.Information("Terminal resized to {Cols}x{Rows} (viewport: {ViewportWidth}x{ViewportHeight}, cell: {CellWidth}x{CellHeight}, total with padding: {Width}x{Height})",
                    _options.Cols, _options.Rows, viewportWidth, viewportHeight, cellWidth, cellHeight, _options.Width, _options.Height);
            }

            // 8. Click terminal to give it focus for interactive applications
            await _terminalPage.ClickTerminalAsync();

            // 9. Wait for buffer to contain actual content before starting recording
            // This prevents capturing blank frames at the start
            // Works for both traditional shells (prompt appears in buffer) and TUI apps (content appears in buffer)
            await _terminalPage.WaitForBufferContentAsync();

            // 7. Initialize CDP session for optimized frame capture
            await _terminalPage.InitializeCdpSessionAsync();

            // 7b. Static-output mode: run Exec, wait for output to settle, then emit a single
            // static frame per Output - no frame-capture loop, no SMIL animation, no command echo.
            if (_options.StaticOutput)
            {
                return await RenderStaticOutputAsync(execCommands, stopwatch, progress, cancellationToken);
            }

            // 8. Initialize frame storage
            _frameStorage = new FrameStorage();

            // 9. Create a shared stopwatch for timing synchronization between FrameCapture and ActivityMonitor
            var sharedStopwatch = new Stopwatch();

            // 10. Create activity monitor with the shared stopwatch
            _activityMonitor = new ActivityMonitor(_terminalPage, _state, sharedStopwatch);

            // Initialize baseline to capture current buffer content before monitoring starts
            // This ensures only NEW content (appearing after recording starts) is detected as "first activity"
            await _activityMonitor.InitializeBaselineAsync();

            // 11. Create FrameCapture with ActivityMonitor and shared stopwatch
            _frameCapture = new FrameCapture(_terminalPage, _options, _state, _frameStorage, _activityMonitor, sharedStopwatch);

            // 12. Start activity monitor first (begins polling for activity)
            _activityMonitor.Start();

            // 13. Start frame capture loop (stopwatch starts here)
            await _frameCapture.StartAsync(cancellationToken);
            VcrLogger.Logger.Debug("Frame capture started, recording in progress");

            progress?.Report("Recording tape...");

            // 15. Execute commands sequentially
            await ExecuteCommandsAsync(commands, cancellationToken, progress);

            // 16. If Exec commands were present, wait for terminal to become inactive
            if (execCommands.Count > 0)
            {
                progress?.Report("Waiting for commands to complete...");
                await WaitForInactivityAsync(cancellationToken);
            }

            // 17. Stop activity monitor
            if (_activityMonitor != null)
            {
                await _activityMonitor.StopAsync();
            }

            // 18. Stop frame capture
            await _frameCapture.StopAsync();

            // 19. Trim frames based on activity frame numbers.
            // Always record the trimmed range so the SVG encoder can window its snapshots, but only
            // physically trim/renumber the PNG layer files when a raster output actually needs them
            // (an SVG-only recording never wrote any PNG frames).
            var frameTrimmer = new FrameTrimmer(_options, _state);
            var frameRange = frameTrimmer.CalculateFrameRange();
            if (frameRange.HasValue)
            {
                // Store the trimmed frame range in options so encoders can access it
                // These are the ORIGINAL frame numbers before renumbering
                _options.TrimmedFirstFrame = frameRange.Value.firstFrame;
                _options.TrimmedLastFrame = frameRange.Value.lastFrame;

                if (_options.RequiresRasterFrames)
                {
                    progress?.Report("Trimming frames based on activity...");

                    frameTrimmer.TrimFrames(_frameStorage.FrameDirectory, frameRange.Value.firstFrame, frameRange.Value.lastFrame);
                    frameTrimmer.RenumberFrames(_frameStorage.FrameDirectory);

                    // Update frame count and elapsed time after trimming
                    var remainingFrames = Directory.GetFiles(_frameStorage.FrameDirectory, "frame-text-*.png").Length;
                    _state.FramesCaptured = remainingFrames;

                    // Recalculate elapsed time based on trimmed frame count and framerate
                    _state.ElapsedTime = TimeSpan.FromSeconds(remainingFrames / (double)_options.Framerate);
                }
            }

            // 20. Generate frames manifest with variable durations (only needed by the raster/FFmpeg
            // encoders; the SVG encoder works directly from terminal-content snapshots).
            if (_options.RequiresRasterFrames)
            {
                progress?.Report("Generating frames manifest...");
                var defaultFrameInterval = TimeSpan.FromSeconds(1.0 / _options.Framerate);
                _frameStorage.GenerateFramesManifest(defaultFrameInterval);
            }

            stopwatch.Stop();

            // Calculate actual achieved framerate from recording

            // 21. Render videos using frames manifest (only if output files were specified)
            var outputFiles = new List<string>();
            if (_options.OutputFiles.Count > 0)
            {
                var videoEncoder = new VideoEncoder(_options, _frameStorage);
                outputFiles = await videoEncoder.RenderAsync(progress);

                // Cleanup frames immediately after encoding completes
                CleanupFrames();
            }

            return new RecordingResult
            {
                FrameDirectory = _frameStorage.FrameDirectory,
                FrameCount = _state.FramesCaptured,
                Duration = stopwatch.Elapsed,
                OutputFiles = outputFiles,
                ScreenshotFiles = _state.ScreenshotFiles
            };
        }
        catch (Exception ex)
        {
            // Cleanup frames even if recording fails
            CleanupFrames();
            throw new VcrSessionException($"Recording failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Records an interactive shell session by capturing the user's keystrokes and writing them out as
    /// a <c>.tape</c> file. Opens a headed browser terminal window the user types into; the session ends
    /// when the user exits the shell (e.g. <c>exit</c> / Ctrl+D) or closes the window.
    /// </summary>
    /// <param name="outputTapePath">Path to write the generated tape file.</param>
    /// <param name="progress">Optional progress reporter for status updates.</param>
    /// <param name="cancellationToken">Cancellation token. On cancellation, a best-effort partial tape is still written.</param>
    /// <returns>Recording result with the generated tape file path.</returns>
    public async Task<RecordingResult> RecordInteractiveAsync(
        string outputTapePath,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            progress?.Report("Starting interactive recording session...");

            // 1. Start ttyd with an interactive shell. An empty Exec list triggers ttyd's interactive
            //    branch (pwsh -NoExit, cmd /k, exec bash, ...). "once: true" makes ttyd exit when the
            //    single client disconnects, so we can detect end-of-session via IsRunning.
            var shellConfig = ShellConfiguration.GetConfiguration(_options.Shell);
            var shellCommand = shellConfig.BuildTtydCommand();

            var environmentVariables = new Dictionary<string, string>(shellConfig.Environment);
            foreach (var (key, value) in _options.Environment)
            {
                environmentVariables[key] = value;
            }
            if (_options.Cols.HasValue)
            {
                environmentVariables["COLUMNS"] = _options.Cols.Value.ToString();
            }
            if (_options.Rows.HasValue)
            {
                environmentVariables["LINES"] = _options.Rows.Value.ToString();
            }

            _ttydProcess = new TtydProcess(
                shellCommand,
                shellConfig,
                execCommands: [],
                _options.WorkingDirectory,
                environmentVariables,
                _options.StartupDelay,
                once: true);
            await _ttydProcess.StartAsync();

            // 2. Launch the browser HEADED at a comfortable window size so the user can type into the
            //    terminal window and freely resize it.
            _browser = new PlaywrightBrowser();
            await _browser.LaunchAsync(headless: false, windowSize: (_options.Width, _options.Height));

            // 3. Create page and navigate to ttyd. noViewport lets the page track the real window size,
            //    so resizing the window propagates to ttyd, which re-fits the terminal (and the bottom
            //    line renders fully). We deliberately do NOT pin a viewport or fixed cols/rows here.
            var url = $"http://localhost:{_ttydProcess.Port}";
            var page = await _browser.NewPageAsync(url, noViewport: true);
            _terminalPage = new TerminalPage(page);

            // 4. Wait for terminal init, then apply theme colors only (no font/size changes — see method).
            await _terminalPage.WaitForTerminalReadyAsync();
            await ConfigureInteractiveTerminalAsync();

            // 5. Bring the window forward and focus the terminal so keystrokes reach the shell.
            await _terminalPage.FocusWindowAsync();
            await _terminalPage.ClickTerminalAsync();
            await _terminalPage.WaitForBufferContentAsync();

            // 7. Start capturing the user's keystrokes (this marks t = 0 for timestamps).
            await _terminalPage.StartInputCaptureAsync();

            progress?.Report("Recording — type in the terminal window. Type `exit` (or Ctrl+D) to finish.");

            // 8. Drain periodically while the session is live so closing the window loses minimal input.
            //    Timestamps are relative to a single in-browser origin, so they stay consistent across drains.
            var captured = new List<InputEvent>();
            try
            {
                while (!cancellationToken.IsCancellationRequested && _ttydProcess.IsRunning)
                {
                    await Task.Delay(250, cancellationToken);
                    captured.AddRange(await _terminalPage.DrainInputCaptureAsync());
                }
            }
            catch (OperationCanceledException)
            {
                _state.IsCancelled = true;
            }

            // 9. Final drain (best effort; the page may already be gone if the window was closed).
            captured.AddRange(await _terminalPage.DrainInputCaptureAsync());

            // 10. Convert to tape text and write the file.
            progress?.Report("Writing tape file...");
            var converterOptions = new InputToTapeOptions
            {
                Shell = _options.Shell,
                DefaultShell = new SessionOptions().Shell,
                Header = _options
            };
            var tape = InputToTapeConverter.Convert(captured, converterOptions);

            var directory = Path.GetDirectoryName(Path.GetFullPath(outputTapePath));
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write without the cancellation token so a cancelled session still saves its partial tape.
            await File.WriteAllTextAsync(outputTapePath, tape);

            stopwatch.Stop();

            return new RecordingResult
            {
                TapeFile = outputTapePath,
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            throw new VcrSessionException($"Interactive recording failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Configures the terminal with theme and visual settings.
    /// </summary>
    private async Task ConfigureTerminalAsync()
    {
        if (_terminalPage == null)
            throw new InvalidOperationException("Terminal page not initialized");

        // Load Google Font if needed
        if (ShouldLoadFromGoogleFonts(_options.FontFamily))
        {
            await _terminalPage.LoadGoogleFontAsync(_options.FontFamily);
        }

        // Set theme colors
        await _terminalPage.SetThemeAsync(BuildThemeColors());

        // Set terminal options
        var terminalOptions = new Dictionary<string, object>
        {
            ["fontSize"] = _options.FontSize,
            ["fontFamily"] = _options.FontFamily,
            ["letterSpacing"] = _options.LetterSpacing,
            ["lineHeight"] = _options.LineHeight,
            ["cursorBlink"] = _options.CursorBlink
        };

        // Enable transparency if requested
        if (_options.TransparentBackground)
        {
            terminalOptions["allowTransparency"] = true;
        }

        await _terminalPage.ConfigureTerminalAsync(terminalOptions);

        // Hide cursor if requested (using CSS injection)
        if (_options.DisableCursor)
        {
            await _terminalPage.HideCursorAsync();
        }
    }

    /// <summary>
    /// Builds the xterm theme color dictionary from the session theme.
    /// </summary>
    private Dictionary<string, string> BuildThemeColors() => new()
    {
        ["background"] = _options.TransparentBackground ? "rgba(0,0,0,0)" : _options.Theme.Background,
        ["foreground"] = _options.Theme.Foreground,
        ["cursor"] = _options.Theme.Cursor,
        ["selectionBackground"] = _options.Theme.SelectionBackground,
        ["black"] = _options.Theme.Black,
        ["red"] = _options.Theme.Red,
        ["green"] = _options.Theme.Green,
        ["yellow"] = _options.Theme.Yellow,
        ["blue"] = _options.Theme.Blue,
        ["magenta"] = _options.Theme.Magenta,
        ["cyan"] = _options.Theme.Cyan,
        ["white"] = _options.Theme.White,
        ["brightBlack"] = _options.Theme.BrightBlack,
        ["brightRed"] = _options.Theme.BrightRed,
        ["brightGreen"] = _options.Theme.BrightGreen,
        ["brightYellow"] = _options.Theme.BrightYellow,
        ["brightBlue"] = _options.Theme.BrightBlue,
        ["brightMagenta"] = _options.Theme.BrightMagenta,
        ["brightCyan"] = _options.Theme.BrightCyan,
        ["brightWhite"] = _options.Theme.BrightWhite
    };

    /// <summary>
    /// Lightweight terminal configuration for interactive record mode: applies theme colors only.
    /// Deliberately skips font-size/family changes and fixed dimensions — the interactive window is
    /// only for the user to type into (it does not affect the captured tape), so letting ttyd render
    /// and fit natively keeps the bottom line fully visible and the window freely resizable.
    /// </summary>
    private async Task ConfigureInteractiveTerminalAsync()
    {
        if (_terminalPage == null)
            throw new InvalidOperationException("Terminal page not initialized");

        await _terminalPage.SetThemeAsync(BuildThemeColors());
    }

    /// <summary>
    /// Determines if a font should be loaded from Google Fonts.
    /// </summary>
    /// <param name="fontFamily">The font family name.</param>
    /// <returns>True if the font should be loaded from Google Fonts.</returns>
    private static bool ShouldLoadFromGoogleFonts(string fontFamily)
    {
        // Always load Cascadia Code from Google Fonts to ensure cross-platform compatibility
        return fontFamily.Equals("Cascadia Code", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Executes all commands sequentially.
    /// </summary>
    private async Task ExecuteCommandsAsync(List<ICommand> commands, CancellationToken cancellationToken, IProgress<string>? progress = null)
    {
        if (_terminalPage == null || _frameCapture == null)
            throw new InvalidOperationException("Session components not initialized");

        var context = new Core.Parsing.Ast.ExecutionContext(_options, _state, _terminalPage, _frameCapture);

        ICommand? previousCommand = null;

        foreach (var command in commands)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _state.IsCancelled = true;
                VcrLogger.Logger.Information("Command execution cancelled");
                break;
            }

            // Skip setting commands (already processed)
            if (command is SetCommand or OutputCommand or RequireCommand or SourceCommand or EnvCommand or ExecCommand)
            {
                continue;
            }

            // Add inter-command delay for consecutive keyboard commands
            if (previousCommand != null &&
                IsKeyboardCommand(previousCommand) &&
                IsKeyboardCommand(command))
            {
                var typingSpeed = _options.TypingSpeed;
                var delayMs = (int)typingSpeed.TotalMilliseconds;

                if (delayMs > 0)
                {
                    VcrLogger.Logger.Debug("Adding {DelayMs}ms inter-command delay between keyboard commands", delayMs);
                    await Task.Delay(delayMs, cancellationToken);
                }
            }

            _state.CurrentCommand = command.ToString();

            // Report progress
            progress?.Report("Recording tape...");

            var commandStopwatch = Stopwatch.StartNew();
            VcrLogger.Logger.Debug("Executing command: {Command}", command);

            try
            {
                await command.ExecuteAsync(context, cancellationToken);
                commandStopwatch.Stop();
                VcrLogger.Logger.Debug("Command completed in {ElapsedMs}ms: {Command}",
                    commandStopwatch.ElapsedMilliseconds, command);

                // Mark current frame as last activity to preserve frames from commands
                // that don't produce terminal output (Sleep, Hide, Show, etc.)
                _activityMonitor?.MarkCurrentFrameAsLastActivity();
            }
            catch (Exception ex)
            {
                commandStopwatch.Stop();
                // Log full exception details including stack trace
                VcrLogger.Logger.Error(ex, "Command '{Command}' failed after {ElapsedMs}ms. Error: {ErrorMessage}",
                    command, commandStopwatch.ElapsedMilliseconds, ex.Message);

                // For keyboard commands, this is critical - don't silently continue
                if (command is KeyCommand)
                {
                    VcrLogger.Logger.Error("Keyboard command failed - this is critical and may indicate input is not working");
                }
            }

            previousCommand = command;
        }

        _state.CurrentCommand = null;
    }

    /// <summary>
    /// Determines if a command is a keyboard-related command that should respect typing delays.
    /// </summary>
    private static bool IsKeyboardCommand(ICommand command)
    {
        return command is KeyCommand or ModifierCommand or TypeCommand;
    }

    /// <summary>
    /// Waits for terminal to become inactive (no output changes).
    /// Used after Exec commands to ensure all output is captured.
    /// </summary>
    private async Task WaitForInactivityAsync(CancellationToken cancellationToken)
    {
        if (_terminalPage == null)
            throw new InvalidOperationException("Terminal page not initialized");

        var inactivityTimeout = _options.InactivityTimeout;
        var maxWaitTime = _options.MaxWaitForInactivity;

        VcrLogger.Logger.Debug("Waiting for terminal inactivity (timeout: {InactivityTimeout}s, max wait: {MaxWait}s)",
            inactivityTimeout.TotalSeconds, maxWaitTime.TotalSeconds);

        // Poll the buffer until it settles (shared with the Screenshot/static-output settle paths).
        var stabilized = await BufferStabilizer.WaitForStableAsync(
            _terminalPage, inactivityTimeout, maxWaitTime, cancellationToken);

        if (stabilized)
        {
            // Stop activity monitor NOW to lock in the last activity frame, preventing the prompt
            // from being counted as activity during the EndBuffer wait.
            if (_activityMonitor != null)
            {
                await _activityMonitor.StopAsync();
            }

            // Wait end buffer to capture final frames.
            await Task.Delay(_options.EndBuffer, cancellationToken);
        }
        else
        {
            VcrLogger.Logger.Debug("Maximum wait time reached, stopping inactivity wait");
        }

        VcrLogger.Logger.Debug("Terminal inactivity wait complete");
    }

    /// <summary>
    /// Renders static-output mode: waits for the program output to settle, then captures a single
    /// settled frame for each Output (static SVG via SvgRenderer, raster PNG via the terminal page).
    /// No frame-capture loop, animation, trimming, or video encoding is involved.
    /// </summary>
    private async Task<RecordingResult> RenderStaticOutputAsync(
        List<ExecCommand> execCommands,
        Stopwatch stopwatch,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (_terminalPage == null)
            throw new InvalidOperationException("Terminal page not initialized");

        // Wait for the program(s) to produce output and settle before snapshotting.
        // minWait = StartupDelay so we don't settle on the pre-Exec prompt before the command runs.
        if (execCommands.Count > 0)
        {
            progress?.Report("Waiting for command output to settle...");
            await BufferStabilizer.WaitForStableAsync(
                _terminalPage,
                _options.InactivityTimeout,
                _options.MaxWaitForInactivity,
                cancellationToken,
                minWait: _options.StartupDelay);
        }

        progress?.Report("Capturing static output...");
        var outputFiles = new List<string>();
        foreach (var outputPath in _options.OutputFiles)
        {
            var extension = Path.GetExtension(outputPath);
            if (extension.Equals(".svg", StringComparison.OrdinalIgnoreCase))
            {
                var content = await _terminalPage.GetTerminalContentWithStylesAsync();
                if (content == null)
                    throw new VcrSessionException("Failed to capture terminal content for static SVG output");

                var renderer = new SvgRenderer(_options);
                if (_options.FitToContent)
                {
                    var extent = ContentExtent.Measure(content);
                    renderer.SetContentExtent(extent.Cols, extent.Rows);
                }
                await renderer.RenderStaticAsync(outputPath, content, cancellationToken);
            }
            else
            {
                // PNG (or any other extension): raster screenshot of the settled buffer.
                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                await _terminalPage.ScreenshotAsync(outputPath, captureCursor: !_options.DisableCursor);
            }

            outputFiles.Add(outputPath);
        }

        stopwatch.Stop();

        return new RecordingResult
        {
            FrameDirectory = string.Empty,
            FrameCount = 0,
            Duration = stopwatch.Elapsed,
            OutputFiles = outputFiles,
            ScreenshotFiles = _state.ScreenshotFiles
        };
    }

    /// <summary>
    /// Explicitly cleans up frame storage after video encoding completes.
    /// Should be called after VideoEncoder.RenderAsync() to ensure FFmpeg has finished reading frames.
    /// </summary>
    public void CleanupFrames()
    {
        if (_framesCleanedUp)
            return;

        _frameStorage?.Dispose();
        _framesCleanedUp = true;
        VcrLogger.Logger.Debug("Frame storage cleaned up explicitly");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        try
        {
            // Stop activity monitor if still running
            if (_activityMonitor != null)
            {
                await _activityMonitor.StopAsync();
                _activityMonitor.Dispose();
            }

            // Stop frame capture if still running
            if (_frameCapture != null)
            {
                await _frameCapture.StopAsync();
            }

            // Close CDP session
            if (_terminalPage != null)
            {
                await _terminalPage.CloseCdpSessionAsync();
            }

            // Close browser
            if (_browser != null)
            {
                await _browser.CloseAsync();
                _browser.Dispose();
            }

            // Stop ttyd
            _ttydProcess?.Dispose();

            // Clean up frame storage (only if not already cleaned up explicitly)
            if (!_framesCleanedUp)
            {
                _frameStorage?.Dispose();
            }
        }
        catch (Exception ex)
        {
            // Log cleanup errors but don't throw during disposal
            VcrLogger.Logger.Warning(ex, "Error during session disposal/cleanup. Error: {ErrorMessage}", ex.Message);
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Result of a recording session.
/// </summary>
public class RecordingResult
{
    public string FrameDirectory { get; set; } = string.Empty;
    public int FrameCount { get; set; }
    public TimeSpan Duration { get; set; }
    public List<string> OutputFiles { get; set; } = new();
    public List<string> ScreenshotFiles { get; set; } = new();

    /// <summary>
    /// Path to the generated tape file (interactive record mode). Empty for normal recordings.
    /// </summary>
    public string TapeFile { get; set; } = string.Empty;
}

/// <summary>
/// Exception thrown when a recording session fails.
/// </summary>
public class VcrSessionException : Exception
{
    public VcrSessionException(string message) : base(message) { }
    public VcrSessionException(string message, Exception innerException) : base(message, innerException) { }
}