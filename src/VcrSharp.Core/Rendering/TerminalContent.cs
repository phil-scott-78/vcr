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
