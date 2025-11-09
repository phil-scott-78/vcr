namespace VcrSharp.Core.Session;

/// <summary>
/// Runtime state for a recording session.
/// </summary>
public class SessionState
{
    /// <summary>
    /// Whether frame capture is currently active (affected by Hide/Show commands).
    /// </summary>
    public bool IsCapturing { get; set; } = true;

    /// <summary>
    /// Current command being executed (for progress reporting).
    /// </summary>
    public string? CurrentCommand { get; set; }

    /// <summary>
    /// Number of frames captured so far.
    /// </summary>
    public int FramesCaptured { get; set; }

    /// <summary>
    /// Elapsed time since recording started.
    /// </summary>
    public TimeSpan ElapsedTime { get; set; }

    /// <summary>
    /// Whether the session has been cancelled.
    /// </summary>
    public bool IsCancelled { get; set; }

    /// <summary>
    /// Timestamp of the first terminal buffer activity detected, relative to recording start.
    /// Null if no activity has been detected yet.
    /// </summary>
    public TimeSpan? FirstActivityTimestamp { get; set; }

    /// <summary>
    /// Timestamp of the most recent terminal buffer activity, relative to recording start.
    /// </summary>
    public TimeSpan LastActivityTimestamp { get; set; }

    /// <summary>
    /// Persistent buffer for Wait+Buffer scope.
    /// Accumulates terminal content across Wait commands to prevent missing fast-scrolling text.
    /// When a pattern is found, the buffer is trimmed from the start up to (and including) the match.
    /// </summary>
    public string PersistentBuffer { get; set; } = string.Empty;

    /// <summary>
    /// Last snapshot of terminal buffer content used for delta detection.
    /// Helps identify new content to append to PersistentBuffer.
    /// </summary>
    public string LastBufferSnapshot { get; set; } = string.Empty;
}