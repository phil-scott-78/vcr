namespace VcrSharp.Core.Recording;

/// <summary>
/// Metadata for a single captured frame, including timing information.
/// </summary>
public class FrameMetadata
{
    /// <summary>
    /// The frame number (1-based).
    /// </summary>
    public int FrameNumber { get; set; }

    /// <summary>
    /// Timestamp when this frame was captured, relative to recording start.
    /// </summary>
    public TimeSpan Timestamp { get; set; }

    /// <summary>
    /// Duration this frame should be displayed (calculated as the time until next frame).
    /// For the last frame, this is set to the default frame interval.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Whether this frame should be captured (false when Hide command is active).
    /// </summary>
    public bool IsVisible { get; set; } = true;
}