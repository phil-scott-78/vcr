using System.Text.Json.Serialization;

namespace VcrSharp.Core.Settings;

/// <summary>
/// Represents a terminal color theme.
/// </summary>
public class Theme
{
    /// <summary>
    /// Gets or sets the theme name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "Default";

    /// <summary>
    /// Gets or sets the background color (hex format).
    /// </summary>
    [JsonPropertyName("background")]
    public string Background { get; set; } = "#1e1e1e";

    /// <summary>
    /// Gets or sets the foreground/text color (hex format).
    /// </summary>
    [JsonPropertyName("foreground")]
    public string Foreground { get; set; } = "#d4d4d4";

    /// <summary>
    /// Gets or sets the cursor color (hex format).
    /// </summary>
    [JsonPropertyName("cursor")]
    public string Cursor { get; set; } = "#d4d4d4";

    /// <summary>
    /// Gets or sets the selection background color (hex format).
    /// </summary>
    [JsonPropertyName("selectionBackground")]
    public string SelectionBackground { get; set; } = "#264f78";

    /// <summary>
    /// Gets or sets the ANSI black color (hex format).
    /// </summary>
    [JsonPropertyName("black")]
    public string Black { get; set; } = "#000000";

    /// <summary>
    /// Gets or sets the ANSI red color (hex format).
    /// </summary>
    [JsonPropertyName("red")]
    public string Red { get; set; } = "#cd3131";

    /// <summary>
    /// Gets or sets the ANSI green color (hex format).
    /// </summary>
    [JsonPropertyName("green")]
    public string Green { get; set; } = "#0dbc79";

    /// <summary>
    /// Gets or sets the ANSI yellow color (hex format).
    /// </summary>
    [JsonPropertyName("yellow")]
    public string Yellow { get; set; } = "#e5e510";

    /// <summary>
    /// Gets or sets the ANSI blue color (hex format).
    /// </summary>
    [JsonPropertyName("blue")]
    public string Blue { get; set; } = "#2472c8";

    /// <summary>
    /// Gets or sets the ANSI magenta color (hex format).
    /// </summary>
    [JsonPropertyName("magenta")]
    public string Magenta { get; set; } = "#bc3fbc";

    /// <summary>
    /// Gets or sets the ANSI cyan color (hex format).
    /// </summary>
    [JsonPropertyName("cyan")]
    public string Cyan { get; set; } = "#11a8cd";

    /// <summary>
    /// Gets or sets the ANSI white color (hex format).
    /// </summary>
    [JsonPropertyName("white")]
    public string White { get; set; } = "#e5e5e5";

    /// <summary>
    /// Gets or sets the ANSI bright black color (hex format).
    /// </summary>
    [JsonPropertyName("brightBlack")]
    public string BrightBlack { get; set; } = "#666666";

    /// <summary>
    /// Gets or sets the ANSI bright red color (hex format).
    /// </summary>
    [JsonPropertyName("brightRed")]
    public string BrightRed { get; set; } = "#f14c4c";

    /// <summary>
    /// Gets or sets the ANSI bright green color (hex format).
    /// </summary>
    [JsonPropertyName("brightGreen")]
    public string BrightGreen { get; set; } = "#23d18b";

    /// <summary>
    /// Gets or sets the ANSI bright yellow color (hex format).
    /// </summary>
    [JsonPropertyName("brightYellow")]
    public string BrightYellow { get; set; } = "#f5f543";

    /// <summary>
    /// Gets or sets the ANSI bright blue color (hex format).
    /// </summary>
    [JsonPropertyName("brightBlue")]
    public string BrightBlue { get; set; } = "#3b8eea";

    /// <summary>
    /// Gets or sets the ANSI bright magenta color (hex format).
    /// </summary>
    [JsonPropertyName("brightMagenta")]
    public string BrightMagenta { get; set; } = "#d670d6";

    /// <summary>
    /// Gets or sets the ANSI bright cyan color (hex format).
    /// </summary>
    [JsonPropertyName("brightCyan")]
    public string BrightCyan { get; set; } = "#29b8db";

    /// <summary>
    /// Gets or sets the ANSI bright white color (hex format).
    /// </summary>
    [JsonPropertyName("brightWhite")]
    public string BrightWhite { get; set; } = "#ffffff";
}