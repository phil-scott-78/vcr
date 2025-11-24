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
    /// Initializes the baseline buffer content before starting monitoring.
    /// This prevents pre-existing content from being detected as "first activity".
    /// </summary>
    public async Task InitializeBaselineAsync()
    {
        // Capture current buffer content as baseline
        _lastBufferContent = await _terminalPage.GetBufferContentAsync();

        var isEmpty = string.IsNullOrEmpty(_lastBufferContent);
        var isWhitespaceOnly = !isEmpty && string.IsNullOrWhiteSpace(_lastBufferContent);
        var length = _lastBufferContent.Length;
        var contentPreview = _lastBufferContent.Length > 200
            ? _lastBufferContent.Substring(0, 200).Replace("\n", "\\n").Replace("\r", "\\r") + "..."
            : _lastBufferContent.Replace("\n", "\\n").Replace("\r", "\\r");

        VcrLogger.Logger.Information("ActivityMonitor baseline initialized. Length: {Length}, IsEmpty: {IsEmpty}, IsWhitespaceOnly: {IsWhitespaceOnly}, Content: [{Preview}]",
            length, isEmpty, isWhitespaceOnly, contentPreview);
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
    /// Explicitly marks the current frame as the last activity point.
    /// Used by commands (like Sleep) that don't produce terminal output but should preserve frames.
    /// </summary>
    public void MarkCurrentFrameAsLastActivity()
    {
        var currentTimestamp = _stopwatch.Elapsed;
        _sessionState.LastActivityTimestamp = currentTimestamp;
        _sessionState.LastActivityFrameNumber = _currentFrameNumber;

        VcrLogger.Logger.Verbose("Explicitly marked frame {FrameNumber} (timestamp: {Timestamp}s) as last activity",
            _currentFrameNumber, currentTimestamp.TotalSeconds);
    }

    /// <summary>
    /// Checks if the content change includes meaningful (non-whitespace) differences.
    /// Returns false if only whitespace was added/removed/changed.
    /// </summary>
    /// <param name="oldContent">Previous buffer content.</param>
    /// <param name="newContent">Current buffer content.</param>
    /// <returns>True if meaningful content changed, false if only whitespace changed.</returns>
    private static bool HasMeaningfulContentChange(string oldContent, string newContent)
    {
        // Extract non-whitespace characters for comparison
        var oldMeaningful = new string(oldContent.Where(c => !char.IsWhiteSpace(c)).ToArray());
        var newMeaningful = new string(newContent.Where(c => !char.IsWhiteSpace(c)).ToArray());

        return oldMeaningful != newMeaningful;
    }

    /// <summary>
    /// Main monitoring loop that polls the terminal buffer for changes.
    /// </summary>
    private async Task MonitorLoopAsync()
    {
        // Poll at 20ms to match typical frame rate (50fps), reducing timing lag
        var pollInterval = TimeSpan.FromMilliseconds(20);

        VcrLogger.Logger.Debug("ActivityMonitor loop started (poll interval: {PollIntervalMs}ms)", pollInterval.TotalMilliseconds);

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

                    // Check if this is a meaningful content change (not just whitespace)
                    var hasMeaningfulChange = HasMeaningfulContentChange(_lastBufferContent, currentContent);

                    if (hasMeaningfulChange)
                    {
                        // Record first activity if this is the first change (after baseline initialization)
                        if (!_sessionState.FirstActivityTimestamp.HasValue)
                        {
                            _sessionState.FirstActivityTimestamp = currentTimestamp;
                            _sessionState.FirstActivityFrameNumber = _currentFrameNumber;
                        }

                        // Update last activity timestamp and frame number
                        _sessionState.LastActivityTimestamp = currentTimestamp;
                        _sessionState.LastActivityFrameNumber = _currentFrameNumber;

                        VcrLogger.Logger.Verbose("Activity detected at frame {FrameNumber} (timestamp: {Timestamp}s)",
                            _currentFrameNumber, currentTimestamp.TotalSeconds);
                    }
                    else
                    {
                        // Content changed but only whitespace - don't count as activity
                        VcrLogger.Logger.Verbose("Whitespace-only change detected at frame {FrameNumber} - ignoring for activity tracking",
                            _currentFrameNumber);
                    }

                    // Always update buffer content to avoid repeated checks
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

        var firstTimeSeconds = _sessionState.FirstActivityTimestamp?.TotalSeconds;
        var lastTimeSeconds = _sessionState.LastActivityTimestamp.TotalSeconds;
        VcrLogger.Logger.Information("ActivityMonitor loop ended. First activity: Frame #{FirstFrame} at {FirstTime}s, Last activity: Frame #{LastFrame} at {LastTime}s",
            _sessionState.FirstActivityFrameNumber, firstTimeSeconds,
            _sessionState.LastActivityFrameNumber, lastTimeSeconds);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}