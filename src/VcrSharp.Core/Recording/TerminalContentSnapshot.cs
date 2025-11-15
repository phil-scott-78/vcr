using VcrSharp.Core.Rendering;

namespace VcrSharp.Core.Recording;

/// <summary>
/// Represents a snapshot of terminal content at a specific frame.
/// Used for SVG generation and other text-based outputs.
/// </summary>
public sealed class TerminalContentSnapshot
{
    /// <summary>
    /// Gets or sets the frame number (1-based, matching FrameMetadata).
    /// </summary>
    public int FrameNumber { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this snapshot was captured.
    /// </summary>
    public TimeSpan Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the terminal content (cells, cursor position, dimensions).
    /// </summary>
    public TerminalContent? Content { get; set; }
}
