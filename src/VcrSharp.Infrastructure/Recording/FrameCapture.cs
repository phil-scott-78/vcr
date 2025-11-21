using System.Diagnostics;
using VcrSharp.Core.Logging;
using VcrSharp.Core.Recording;
using VcrSharp.Core.Session;
using VcrSharp.Infrastructure.Playwright;
using VcrSharp.Infrastructure.Rendering;

namespace VcrSharp.Infrastructure.Recording;

/// <summary>
/// Manages frame capture during terminal recording sessions.
/// Implements a high-precision async timer loop to capture frames at the configured framerate.
/// Uses background I/O queue to prevent file writes from blocking the capture loop.
/// </summary>
public class FrameCapture : IFrameCapture, IAsyncDisposable
{
    private readonly TerminalPage _terminalPage;
    private readonly SessionOptions _options;
    private readonly SessionState _state;
    private readonly FrameStorage _storage;
    private readonly FrameWriteQueue _writeQueue;
    private readonly ActivityMonitor? _activityMonitor;
    private Task? _captureTask;
    private CancellationTokenSource? _cancellationTokenSource;

    /// <summary>
    /// Gets whether the capture loop is currently running.
    /// </summary>
    private bool IsRunning => _captureTask is { IsCompleted: false };

    /// <summary>
    /// Gets the stopwatch used for frame timing.
    /// </summary>
    public Stopwatch Stopwatch { get; }

    /// <summary>
    /// Initializes a new instance of FrameCapture.
    /// </summary>
    /// <param name="terminalPage">The terminal page to capture frames from</param>
    /// <param name="options">Session options containing framerate settings</param>
    /// <param name="state">Session state for tracking capture status</param>
    /// <param name="storage">Frame storage manager</param>
    /// <param name="activityMonitor">Activity monitor to notify of frame captures (optional)</param>
    /// <param name="stopwatch">Optional shared stopwatch for timing. If null, a new stopwatch is created.</param>
    public FrameCapture(
        TerminalPage terminalPage,
        SessionOptions options,
        SessionState state,
        FrameStorage storage,
        ActivityMonitor? activityMonitor = null,
        Stopwatch? stopwatch = null)
    {
        ArgumentNullException.ThrowIfNull(terminalPage);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(storage);
        _terminalPage = terminalPage;
        _options = options;
        _state = state;
        _storage = storage;
        _activityMonitor = activityMonitor;
        Stopwatch = stopwatch ?? new Stopwatch();
        _writeQueue = new FrameWriteQueue();
    }

    /// <summary>
    /// Starts the frame capture loop.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the capture loop</param>
    /// <returns>Task that completes when capture loop stops</returns>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
            throw new InvalidOperationException("Capture is already running");

        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Stopwatch.Restart();
        _state.FramesCaptured = 0;

        _captureTask = CaptureLoopAsync(_cancellationTokenSource.Token);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the frame capture loop and flushes all queued frames to disk.
    /// </summary>
    /// <returns>Task that completes when capture loop has stopped and all frames are written</returns>
    public async Task StopAsync()
    {
        if (!IsRunning)
            return;

        if (_cancellationTokenSource != null)
        {
            await _cancellationTokenSource.CancelAsync();
        }

        if (_captureTask != null)
        {
            try
            {
                await _captureTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }
        }

        Stopwatch.Stop();
        _state.ElapsedTime = Stopwatch.Elapsed;

        // Flush all queued frames to disk before returning
        await _writeQueue.CompleteAsync();
    }

    /// <summary>
    /// Captures a single frame immediately and returns the frame number.
    /// Captures text and cursor layers separately and queues them for background writing.
    /// Also captures terminal content with styles for SVG/text-based outputs.
    /// </summary>
    /// <returns>The frame number that was captured</returns>
    private async Task CaptureFrameAsync()
    {
        var frameNumber = _state.FramesCaptured + 1;
        var timestamp = Stopwatch.Elapsed;

        // Capture both PNG layers and terminal content in parallel for better performance
        // Skip cursor layer if DisableCursor is enabled
        var captureLayersTask = _terminalPage.CaptureLayersAsync(captureCursor: !_options.DisableCursor);
        var terminalContentTask = _terminalPage.GetTerminalContentWithStylesAsync();

        await Task.WhenAll(captureLayersTask, terminalContentTask);

        var (textBytes, cursorBytes) = captureLayersTask.Result;
        var terminalContent = terminalContentTask.Result;

        // Get file paths for both layers
        var textPath = _storage.GetFrameLayerPath(frameNumber, "text");
        var cursorPath = _storage.GetFrameLayerPath(frameNumber, "cursor");

        // Enqueue for background writing (non-blocking)
        await _writeQueue.EnqueueAsync(textPath, textBytes);
        await _writeQueue.EnqueueAsync(cursorPath, cursorBytes);

        // Record terminal content snapshot for SVG/text-based outputs
        var snapshot = new TerminalContentSnapshot
        {
            FrameNumber = frameNumber,
            Timestamp = timestamp,
            Content = terminalContent
        };
        _storage.RecordTerminalSnapshot(snapshot);

        // Record frame metadata with timestamp
        var metadata = new FrameMetadata
        {
            FrameNumber = frameNumber,
            Timestamp = timestamp,
            IsVisible = _state.IsCapturing
        };
        _storage.RecordFrameMetadata(metadata);

        _state.FramesCaptured = frameNumber;

        // Notify activity monitor of the captured frame
        _activityMonitor?.NotifyFrameCaptured(frameNumber);
    }

    /// <summary>
    /// Main capture loop that runs at the configured framerate.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the loop</param>
    private async Task CaptureLoopAsync(CancellationToken cancellationToken)
    {
        // Calculate frame interval based on framerate
        var frameInterval = TimeSpan.FromSeconds(1.0 / _options.Framerate);
        var nextFrameTime = Stopwatch.Elapsed;

        while (!cancellationToken.IsCancellationRequested && !_state.IsCancelled)
        {
            try
            {
                // Capture frame if recording is active (not hidden)
                if (_state.IsCapturing)
                {
                    await CaptureFrameAsync();
                }

                // Update elapsed time
                _state.ElapsedTime = Stopwatch.Elapsed;

                // Calculate next frame time
                nextFrameTime += frameInterval;

                // Wait until next frame time (with drift compensation)
                var now = Stopwatch.Elapsed;
                var delay = nextFrameTime - now;

                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken);
                }
                else
                {
                    // We're falling behind, skip the delay but maintain schedule
                    nextFrameTime = now;
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
                break;
            }
            catch (Exception ex)
            {
                // Log error but continue capturing to avoid stopping the entire recording
                VcrLogger.Logger.Error(ex, "Frame capture failed at frame {FrameNumber}. IsCapturing: {IsCapturing}. Error: {ErrorMessage}",
                    _state.FramesCaptured + 1, _state.IsCapturing, ex.Message);
            }
        }
    }

    /// <summary>
    /// Captures a screenshot to a specific file path (for Screenshot command).
    /// Format is auto-detected from file extension (.png for raster, .svg for vector).
    /// </summary>
    /// <param name="path">The file path to save the screenshot</param>
    /// <returns>Task that completes when screenshot is saved</returns>
    public async Task CaptureScreenshotAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        // Auto-detect format from file extension
        var extension = Path.GetExtension(path);

        if (extension.Equals(".svg", StringComparison.OrdinalIgnoreCase))
        {
            // SVG screenshot - capture terminal content and render as SVG
            var content = await _terminalPage.GetTerminalContentWithStylesAsync();

            if (content == null)
            {
                throw new InvalidOperationException("Failed to capture terminal content for SVG screenshot");
            }

            var renderer = new SvgRenderer(_options);
            await renderer.RenderStaticAsync(path, content);
        }
        else
        {
            // PNG screenshot (default for .png or any other extension)
            // Skip cursor layer if DisableCursor is enabled
            await _terminalPage.ScreenshotAsync(path, captureCursor: !_options.DisableCursor);
        }

        // Track screenshot file in session state
        _state.ScreenshotFiles.Add(path);
    }

    /// <summary>
    /// Disposes resources and ensures all queued frames are written.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _writeQueue.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}