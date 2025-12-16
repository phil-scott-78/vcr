using System.Text.Json.Serialization;

namespace VcrSharp.Core.Rendering;

/// <summary>
/// Represents the full terminal content with styling information.
/// Contains a 2D array of terminal cells representing the visible screen.
/// </summary>
public sealed class TerminalContent
{
    /// <summary>
    /// Gets or sets the number of columns.
    /// </summary>
    [JsonPropertyName("cols")]
    public int Cols { get; set; }

    /// <summary>
    /// Gets or sets the number of rows.
    /// </summary>
    [JsonPropertyName("rows")]
    public int Rows { get; set; }

    /// <summary>
    /// Gets or sets the terminal cells (row-major order: Cells[row][col]).
    /// </summary>
    [JsonPropertyName("cells")]
    public TerminalCell[][] Cells { get; set; } = Array.Empty<TerminalCell[]>();

    /// <summary>
    /// Gets or sets the cursor position (column).
    /// </summary>
    [JsonPropertyName("cursorX")]
    public int CursorX { get; set; }

    /// <summary>
    /// Gets or sets the cursor position (row).
    /// </summary>
    [JsonPropertyName("cursorY")]
    public int CursorY { get; set; }

    /// <summary>
    /// Gets or sets whether the cursor is visible.
    /// </summary>
    [JsonPropertyName("cursorVisible")]
    public bool CursorVisible { get; set; }
}

/// <summary>
/// Represents a terminal state at a specific point in time for animated rendering.
/// </summary>
public sealed class TerminalStateWithTime
{
    /// <summary>
    /// The terminal content at this point in time.
    /// </summary>
    public required TerminalContent Content { get; init; }

    /// <summary>
    /// The timestamp in seconds from the start of the recording.
    /// </summary>
    public required double TimestampSeconds { get; init; }

    /// <summary>
    /// Whether the cursor is idle (not blinking) at this point.
    /// </summary>
    public bool IsCursorIdle { get; init; }
}
