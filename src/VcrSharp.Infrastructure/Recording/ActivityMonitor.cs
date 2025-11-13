using System.Diagnostics;
using VcrSharp.Core.Logging;
using VcrSharp.Core.Session;
using VcrSharp.Infrastructure.Playwright;

namespace VcrSharp.Infrastructure.Recording;

/// <summary>
/// Monitors terminal buffer changes to detect first and last activity timestamps.
/// Runs in the background during recording to track when content appears and changes.
/// </summary>
public class ActivityMonitor : IDisposable
{
    private readonly TerminalPage _terminalPage;
    private readonly SessionState _sessionState;
    private readonly Stopwatch _stopwatch;
    private readonly CancellationTokenSource _cts;
    private Task? _monitorTask;
    private string _lastBufferContent = string.Empty;
    private int _currentFrameNumber;

    /// <summary>
    /// Creates a new activity monitor.
    /// </summary>
    /// <param name="terminalPage">The terminal page to monitor.</param>
    /// <param name="sessionState">The session state to update with activity timestamps.</param>
    /// <param name="stopwatch">Shared stopwatch for timing (same as frame capture).</param>
    public ActivityMonitor(
        TerminalPage terminalPage,
        SessionState sessionState,
        Stopwatch stopwatch)
    {
        _terminalPage = terminalPage;
        _sessionState = sessionState;
        _stopwatch = stopwatch;
        _cts = new CancellationTokenSource();
    }

    /// <summary>
    /// Starts monitoring terminal buffer for changes.
    /// </summary>
    public void Start()
    {
        if (_monitorTask != null)
            throw new InvalidOperationException("Activity monitor is already running");

        _monitorTask = Task.Run(MonitorLoopAsync, _cts.Token);
    }

    /// <summary>
    /// Stops monitoring and waits for the monitoring task to complete.
    /// </summary>
    public async Task StopAsync()
    {
        if (_monitorTask == null)
            return;

        await _cts.CancelAsync();

        try
        {
            await _monitorTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelling
        }
    }

    /// <summary>
    /// Called by FrameCapture when a frame is captured.
    /// Updates the current frame number for activity tracking.
    /// </summary>
    /// <param name="frameNumber">The frame number that was just captured.</param>
    public void NotifyFrameCaptured(int frameNumber)
    {
        _currentFrameNumber = frameNumber;
    }

    /// <summary>
    /// Main monitoring loop that polls the terminal buffer for changes.
    /// </summary>
    private async Task MonitorLoopAsync()
    {
        // Poll at 20ms to match typical frame rate (50fps), reducing timing lag
        var pollInterval = TimeSpan.FromMilliseconds(20);

        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                // Get current buffer content
                var currentContent = await _terminalPage.GetBufferContentAsync();

                // Check if content has changed
                if (currentContent != _lastBufferContent)
                {
                    var currentTimestamp = _stopwatch.Elapsed;

                    // Record first activity if this is the first change
                    if (!_sessionState.FirstActivityTimestamp.HasValue)
                    {
                        _sessionState.FirstActivityTimestamp = currentTimestamp;
                        _sessionState.FirstActivityFrameNumber = _currentFrameNumber;
                    }

                    // Always update last activity timestamp and frame number
                    _sessionState.LastActivityTimestamp = currentTimestamp;
                    _sessionState.LastActivityFrameNumber = _currentFrameNumber;

                    _lastBufferContent = currentContent;
                }

                // Wait before next poll
                await Task.Delay(pollInterval, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Exit gracefully
                break;
            }
            catch (Exception ex)
            {
                // Log errors but continue monitoring to avoid stopping activity tracking
                VcrLogger.Logger.Error(ex, "Failed to read terminal buffer for activity monitoring. Frame: {FrameNumber}. Error: {ErrorMessage}",
                    _currentFrameNumber, ex.Message);
                await Task.Delay(pollInterval, _cts.Token);
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}