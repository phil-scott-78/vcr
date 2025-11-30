using System.Text.Json.Serialization;

namespace VcrSharp.Core.Rendering;

/// <summary>
/// Represents a single terminal cell with character and style information.
/// </summary>
public sealed class TerminalCell
{
    /// <summary>
    /// Gets or sets the character in this cell.
    /// Stored as string to support Unicode surrogate pairs (emojis, etc.).
    /// </summary>
    [JsonPropertyName("character")]
    public string Character { get; set; } = " ";

    /// <summary>
    /// Gets or sets the foreground color (hex format like "#FFFFFF" or ANSI color index).
    /// </summary>
    [JsonPropertyName("foregroundColor")]
    public string? ForegroundColor { get; set; }

    /// <summary>
    /// Gets or sets the background color (hex format like "#000000" or ANSI color index).
    /// </summary>
    [JsonPropertyName("backgroundColor")]
    public string? BackgroundColor { get; set; }

    /// <summary>
    /// Gets or sets whether the text is bold.
    /// </summary>
    [JsonPropertyName("isBold")]
    public bool IsBold { get; set; }

    /// <summary>
    /// Gets or sets whether the text is italic.
    /// </summary>
    [JsonPropertyName("isItalic")]
    public bool IsItalic { get; set; }

    /// <summary>
    /// Gets or sets whether the text is underlined.
    /// </summary>
    [JsonPropertyName("isUnderline")]
    public bool IsUnderline { get; set; }

    /// <summary>
    /// Gets or sets whether this cell contains the cursor.
    /// </summary>
    [JsonPropertyName("isCursor")]
    public bool IsCursor { get; set; }

    /// <summary>
    /// Gets or sets the display width of this cell in terminal columns.
    /// Normal characters have width 1, wide characters (emojis, CJK, certain symbols) have width 2.
    /// Continuation cells (second column of a wide character) have width 0.
    /// </summary>
    [JsonPropertyName("width")]
    public int Width { get; set; } = 1;
}
