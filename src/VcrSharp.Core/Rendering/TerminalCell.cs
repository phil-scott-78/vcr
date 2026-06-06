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
    /// Gets or sets whether the text is underlined. Derived from <see cref="UnderlineStyle"/> &gt; 0
    /// (kept for back-compat with renderers that only understand a boolean underline).
    /// </summary>
    [JsonPropertyName("isUnderline")]
    public bool IsUnderline { get; set; }

    /// <summary>Underline style: 0 none, 1 single, 2 double, 3 curly, 4 dotted, 5 dashed (SGR 4 / 4:n / 21).</summary>
    [JsonPropertyName("underlineStyle")]
    public int UnderlineStyle { get; set; }

    /// <summary>Underline color (SGR 58/59), independent of foreground; null follows the foreground.</summary>
    [JsonPropertyName("underlineColor")]
    public string? UnderlineColor { get; set; }

    /// <summary>Faint / dim (SGR 2).</summary>
    [JsonPropertyName("isDim")]
    public bool IsDim { get; set; }

    /// <summary>Blink (SGR 5/6). Rendered steady in static SVG.</summary>
    [JsonPropertyName("isBlink")]
    public bool IsBlink { get; set; }

    /// <summary>Reverse / negative video (SGR 7) — swap fg/bg at render time.</summary>
    [JsonPropertyName("isReverse")]
    public bool IsReverse { get; set; }

    /// <summary>Conceal / hidden (SGR 8).</summary>
    [JsonPropertyName("isConceal")]
    public bool IsConceal { get; set; }

    /// <summary>Strikethrough (SGR 9).</summary>
    [JsonPropertyName("isStrikethrough")]
    public bool IsStrikethrough { get; set; }

    /// <summary>Overline (SGR 53).</summary>
    [JsonPropertyName("isOverline")]
    public bool IsOverline { get; set; }

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
