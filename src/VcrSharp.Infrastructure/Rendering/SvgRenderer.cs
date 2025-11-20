using System.Globalization;
using System.Text;
using System.Xml;
using VcrSharp.Core.Recording;
using VcrSharp.Core.Rendering;
using VcrSharp.Core.Session;

namespace VcrSharp.Infrastructure.Rendering;

/// <summary>
/// Shared SVG rendering functionality for both animated recordings and static screenshots.
/// Provides text-based SVG generation with terminal styling support.
/// </summary>
public class SvgRenderer
{
    private readonly SessionOptions _options;

    // Dimension calculations
    private double _charWidth;
    private double _charHeight;
    private int _frameWidth;
    private int _frameHeight;

    public SvgRenderer(SessionOptions options)
    {
        _options = options;
        CalculateDimensions();
    }

    /// <summary>
    /// Renders a single terminal state as a static SVG (no animations).
    /// </summary>
    public async Task RenderStaticAsync(string outputPath, TerminalContent content, CancellationToken cancellationToken = default)
    {
        // Ensure output directory exists
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);
        await using var xml = XmlWriter.Create(writer, new XmlWriterSettings
        {
            Async = true,
            Indent = false,
            OmitXmlDeclaration = true,
            Encoding = Encoding.UTF8
        });

        // Outer SVG with viewBox for responsive scaling
        await xml.WriteStartElementAsync(null, "svg", "http://www.w3.org/2000/svg");
        await xml.WriteAttributeStringAsync(null, "viewBox", null, $"0 0 {_options.Width} {_options.Height}");

        // Styles (no animations for static SVG)
        await WriteStaticStylesAsync(xml);

        // Background (skip if transparent background is enabled)
        if (!_options.TransparentBackground)
        {
            await xml.WriteStartElementAsync(null, "rect", null);
            await xml.WriteAttributeStringAsync(null, "width", null, "100%");
            await xml.WriteAttributeStringAsync(null, "height", null, "100%");
            await xml.WriteAttributeStringAsync(null, "fill", null, OptimizeHexColor(_options.Theme.Background));
            await xml.WriteEndElementAsync();
        }

        // Inner SVG with viewBox (shows the terminal content)
        await xml.WriteStartElementAsync(null, "svg", null);
        await xml.WriteAttributeStringAsync(null, "x", null, FormatNumber(_options.Padding));
        await xml.WriteAttributeStringAsync(null, "y", null, FormatNumber(_options.Padding));
        await xml.WriteAttributeStringAsync(null, "width", null, _frameWidth.ToString(CultureInfo.InvariantCulture));
        await xml.WriteAttributeStringAsync(null, "height", null, _frameHeight.ToString(CultureInfo.InvariantCulture));
        await xml.WriteAttributeStringAsync(null, "viewBox", null, $"0 0 {_frameWidth} {_frameHeight}");

        // Content group with xml:space
        await xml.WriteStartElementAsync(null, "g", null);
        await xml.WriteAttributeStringAsync("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");

        // Render each line
        for (var row = 0; row < content.Rows; row++)
        {
            await RenderLineAsync(xml, content, row, isCursorIdle: false);
        }

        await xml.WriteEndElementAsync(); // g
        await xml.WriteEndElementAsync(); // svg inner
        await xml.WriteEndElementAsync(); // svg outer

        await xml.FlushAsync();
    }

    /// <summary>
    /// Renders multiple terminal states as an animated SVG.
    /// </summary>
    public async Task RenderAnimatedAsync(
        string outputPath,
        IReadOnlyList<TerminalStateWithTime> states,
        double totalDurationSeconds,
        CancellationToken cancellationToken = default)
    {
        // Ensure output directory exists
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);
        await using var xml = XmlWriter.Create(writer, new XmlWriterSettings
        {
            Async = true,
            Indent = false,
            OmitXmlDeclaration = true,
            Encoding = Encoding.UTF8
        });

        // Outer SVG with viewBox for responsive scaling
        await xml.WriteStartElementAsync(null, "svg", "http://www.w3.org/2000/svg");
        await xml.WriteAttributeStringAsync(null, "viewBox", null, $"0 0 {_options.Width} {_options.Height}");

        // Styles and animations
        await WriteAnimatedStylesAsync(xml, states, totalDurationSeconds);

        // Background (skip if transparent background is enabled)
        if (!_options.TransparentBackground)
        {
            await xml.WriteStartElementAsync(null, "rect", null);
            await xml.WriteAttributeStringAsync(null, "width", null, "100%");
            await xml.WriteAttributeStringAsync(null, "height", null, "100%");
            await xml.WriteAttributeStringAsync(null, "fill", null, OptimizeHexColor(_options.Theme.Background));
            await xml.WriteEndElementAsync();
        }

        // Inner SVG with viewBox (shows one frame at a time)
        await xml.WriteStartElementAsync(null, "svg", null);
        await xml.WriteAttributeStringAsync(null, "x", null, FormatNumber(_options.Padding));
        await xml.WriteAttributeStringAsync(null, "y", null, FormatNumber(_options.Padding));
        await xml.WriteAttributeStringAsync(null, "width", null, _frameWidth.ToString(CultureInfo.InvariantCulture));
        await xml.WriteAttributeStringAsync(null, "height", null, _frameHeight.ToString(CultureInfo.InvariantCulture));
        await xml.WriteAttributeStringAsync(null, "viewBox", null, $"0 0 {_frameWidth} {_frameHeight}");

        // Animation container with translateX
        await xml.WriteStartElementAsync(null, "g", null);
        await xml.WriteAttributeStringAsync(null, "class", null, "animation-container");

        // Render each unique state
        for (var i = 0; i < states.Count; i++)
        {
            var state = states[i];
            var xOffset = i * _frameWidth;

            await RenderTerminalStateAsync(xml, state.Content, xOffset, state.IsCursorIdle);
        }

        await xml.WriteEndElementAsync(); // g animation-container
        await xml.WriteEndElementAsync(); // svg inner
        await xml.WriteEndElementAsync(); // svg outer

        await xml.FlushAsync();
    }

    /// <summary>
    /// Calculates character and frame dimensions based on session options.
    /// </summary>
    private void CalculateDimensions()
    {
        // Use actual measured cell dimensions from xterm.js if available,
        // otherwise fall back to estimation based on font size
        // Note: 0.55 multiplier matches VHS implementation to prevent cursor drift
        _charWidth = _options.ActualCellWidth ?? _options.FontSize * 0.55;
        _charHeight = _options.ActualCellHeight ?? _options.FontSize * 1.2;

        // Frame dimensions (terminal area without padding)
        _frameWidth = _options.Width - 2 * _options.Padding;
        _frameHeight = _options.Height - 2 * _options.Padding;
    }

    /// <summary>
    /// Writes CSS styles for static SVG (no animations).
    /// </summary>
    private async Task WriteStaticStylesAsync(XmlWriter xml)
    {
        await xml.WriteStartElementAsync(null, "style", null);

        var css = new StringBuilder();

        // Base styles (minified - no newlines between rules)
        css.Append($"text{{white-space:pre;font-family:{_options.FontFamily};font-size:{_options.FontSize}px;letter-spacing:0;word-spacing:0;text-rendering:geometricPrecision;font-variant-ligatures:none;dominant-baseline:text-before-edge}}");
        css.Append($".fg{{fill:{OptimizeHexColor(_options.Theme.Foreground)}}}");

        // ANSI color classes
        AppendAnsiColorStyles(css);

        // Style flags (minified)
        css.Append(".bold{font-weight:bold}.italic{font-style:italic}.underline{text-decoration:underline}");

        // Cursor styles (minified, no animation for static)
        if (!_options.DisableCursor)
        {
            css.Append($".cursor-block{{fill:{OptimizeHexColor(_options.Theme.Foreground)}}}");
        }

        await xml.WriteStringAsync(css.ToString());
        await xml.WriteEndElementAsync(); // style
    }

    /// <summary>
    /// Writes CSS styles and keyframe animations for animated SVG.
    /// </summary>
    private async Task WriteAnimatedStylesAsync(
        XmlWriter xml,
        IReadOnlyList<TerminalStateWithTime> states,
        double totalDurationSeconds)
    {
        await xml.WriteStartElementAsync(null, "style", null);

        var css = new StringBuilder();

        // Base styles (minified - no newlines between rules)
        css.Append($"text{{white-space:pre;font-family:{_options.FontFamily};font-size:{_options.FontSize}px;letter-spacing:0;word-spacing:0;text-rendering:geometricPrecision;font-variant-ligatures:none;dominant-baseline:text-before-edge}}");
        css.Append($".fg{{fill:{OptimizeHexColor(_options.Theme.Foreground)}}}");

        // ANSI color classes
        AppendAnsiColorStyles(css);

        // Style flags (minified)
        css.Append(".bold{font-weight:bold}.italic{font-style:italic}.underline{text-decoration:underline}");

        // Cursor styles (minified)
        if (!_options.DisableCursor)
        {
            css.Append("@keyframes blink{0%,49%{opacity:1}50%,100%{opacity:0}}");
            css.Append(".cursor-idle{animation:blink 1s steps(1) infinite}");
            css.Append($".cursor-block{{fill:{OptimizeHexColor(_options.Theme.Foreground)}}}");
        }

        // Slide animation keyframes
        if (states.Count > 0)
        {
            css.AppendLine("@keyframes slide{");

            int? lastStateIndex = null;
            for (var i = 0; i < states.Count; i++)
            {
                var state = states[i];

                // Skip consecutive duplicate states to reduce file size
                if (lastStateIndex.HasValue && i == lastStateIndex.Value)
                    continue;

                var percentage = state.TimestampSeconds / totalDurationSeconds * 100.0;
                var offset = -1 * i * _frameWidth;

                // Use adaptive precision (reduced from 4 to 3 for file size)
                var precision = states.Count < 100 ? 1 : states.Count < 1000 ? 2 : 3;
                var percentStr = percentage.ToString("F" + precision, CultureInfo.InvariantCulture);

                // Omit 'px' unit for zero values to reduce file size
                var offsetStr = offset == 0 ? "0" : $"{offset}px";
                css.AppendLine($"{percentStr}%{{transform:translateX({offsetStr});}}");
                lastStateIndex = i;
            }

            css.AppendLine("}");

            // Apply animation to container (minified)
            var loopOffset = _options.LoopOffset;
            css.Append($".animation-container{{animation:slide {totalDurationSeconds}s step-end {loopOffset}s infinite}}");
        }

        await xml.WriteStringAsync(css.ToString());
        await xml.WriteEndElementAsync(); // style
    }

    /// <summary>
    /// Appends ANSI color styles to CSS builder.
    /// </summary>
    private void AppendAnsiColorStyles(StringBuilder css)
    {
        var colors = new Dictionary<string, string>
        {
            ["k"] = _options.Theme.Black,
            ["r"] = _options.Theme.Red,
            ["g"] = _options.Theme.Green,
            ["y"] = _options.Theme.Yellow,
            ["b"] = _options.Theme.Blue,
            ["m"] = _options.Theme.Magenta,
            ["c"] = _options.Theme.Cyan,
            ["w"] = _options.Theme.White,
            ["K"] = _options.Theme.BrightBlack,
            ["R"] = _options.Theme.BrightRed,
            ["G"] = _options.Theme.BrightGreen,
            ["Y"] = _options.Theme.BrightYellow,
            ["B"] = _options.Theme.BrightBlue,
            ["M"] = _options.Theme.BrightMagenta,
            ["C"] = _options.Theme.BrightCyan,
            ["W"] = _options.Theme.BrightWhite
        };

        foreach (var (name, color) in colors)
        {
            css.Append($".{name}{{fill:{OptimizeHexColor(color)}}}");
        }
    }

    /// <summary>
    /// Renders a single terminal state as SVG.
    /// </summary>
    private async Task RenderTerminalStateAsync(
        XmlWriter xml,
        TerminalContent content,
        int xOffset,
        bool isCursorIdle)
    {
        // State group with xml:space to avoid repeating on every text element
        await xml.WriteStartElementAsync(null, "g", null);
        await xml.WriteAttributeStringAsync(null, "transform", null, $"translate({xOffset},0)");
        await xml.WriteAttributeStringAsync("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");

        // Render each line
        for (var row = 0; row < content.Rows; row++)
        {
            await RenderLineAsync(xml, content, row, isCursorIdle);
        }

        await xml.WriteEndElementAsync(); // g state
    }

    /// <summary>
    /// Renders a single line of terminal content.
    /// </summary>
    private async Task RenderLineAsync(XmlWriter xml, TerminalContent content, int row, bool isCursorIdle)
    {
        var y = (row + 1) * _charHeight; // Baseline position

        // Group consecutive cells with same styling
        var runs = BuildStyleRuns(content.Cells[row], row, content.CursorY, content.CursorX, isCursorIdle);

        if (runs.Count == 0)
            return;

        // Skip completely empty lines (no text, no background, no cursor) to reduce file size
        if (runs.Count == 1 && string.IsNullOrWhiteSpace(runs[0].Text) &&
            runs[0].BackgroundColor == null && !runs[0].IsCursor)
            return;

        // Render backgrounds first (if any)
        await RenderBackgroundsAsync(xml, runs, row);

        // Calculate total text length for this line
        var totalChars = runs.Sum(r => r.Text.Length);
        var textLength = totalChars * _charWidth;

        // Render text with exact length enforcement to prevent drift
        await xml.WriteStartElementAsync(null, "text", null);
        await xml.WriteAttributeStringAsync(null, "y", null, FormatNumber(y));
        await xml.WriteAttributeStringAsync(null, "textLength", null, FormatNumber(textLength));
        await xml.WriteAttributeStringAsync(null, "lengthAdjust", null, "spacing");

        for (var i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            await xml.WriteStartElementAsync(null, "tspan", null);

            // First tspan gets x="0"
            if (i == 0)
            {
                await xml.WriteAttributeStringAsync(null, "x", null, "0");
            }

            // Apply styling
            var classes = BuildCssClasses(run, _options.DisableCursor);
            if (!string.IsNullOrEmpty(classes))
            {
                await xml.WriteAttributeStringAsync(null, "class", null, classes);
            }

            // Inline color if not using class
            if (run.ForegroundColor != null)
            {
                string? inlineColor = null;

                // RGB colors (start with #)
                if (run.ForegroundColor.StartsWith('#'))
                {
                    inlineColor = OptimizeHexColor(run.ForegroundColor);
                }
                // Extended palette colors (16-255) - convert to RGB
                else if (int.TryParse(run.ForegroundColor, out var paletteIndex) && paletteIndex >= 16)
                {
                    inlineColor = PaletteIndexToRgb(paletteIndex);
                }

                if (inlineColor != null)
                {
                    await xml.WriteAttributeStringAsync(null, "fill", null, inlineColor);
                }
            }

            await xml.WriteStringAsync(run.Text);
            await xml.WriteEndElementAsync(); // tspan
        }

        await xml.WriteEndElementAsync(); // text
    }

    /// <summary>
    /// Renders background rectangles for cells with background colors or cursor.
    /// Consolidates consecutive cells with the same background color into single wider rectangles.
    /// </summary>
    private async Task RenderBackgroundsAsync(XmlWriter xml, List<StyleRun> runs, int row)
    {
        var col = 0;
        string? lastBgColor = null;
        var bgStartCol = 0;
        var bgLength = 0;

        foreach (var run in runs)
        {
            // Handle background color consolidation
            if (run.BackgroundColor != null)
            {
                if (run.BackgroundColor == lastBgColor)
                {
                    // Same background color - extend the current run
                    bgLength += run.Text.Length;
                }
                else
                {
                    // Different background color - render accumulated background if any
                    if (lastBgColor != null)
                    {
                        await RenderBackgroundRectAsync(xml, row, bgStartCol, bgLength, lastBgColor);
                    }

                    // Start new background run
                    lastBgColor = run.BackgroundColor;
                    bgStartCol = col;
                    bgLength = run.Text.Length;
                }
            }
            else
            {
                // No background - render accumulated background if any
                if (lastBgColor != null)
                {
                    await RenderBackgroundRectAsync(xml, row, bgStartCol, bgLength, lastBgColor);
                    lastBgColor = null;
                }
            }

            // Render cursor background (only when cursor is active/visible and not disabled)
            if (!_options.DisableCursor && run is { IsCursor: true, IsCursorIdle: false })
            {
                var x = col * _charWidth;
                // Align cursor with text visual bounds (text baseline is at (row + 1) * _charHeight)
                var y = (row + 1) * _charHeight - _charHeight * 0.85;
                var width = _charWidth; // Cursor is always 1 character wide

                await xml.WriteStartElementAsync(null, "rect", null);
                await xml.WriteAttributeStringAsync(null, "x", null, FormatNumber(x));
                await xml.WriteAttributeStringAsync(null, "y", null, FormatNumber(y));
                await xml.WriteAttributeStringAsync(null, "width", null, FormatNumber(width));
                await xml.WriteAttributeStringAsync(null, "height", null, FormatNumber(_charHeight));
                await xml.WriteAttributeStringAsync(null, "class", null, "cursor-block");
                await xml.WriteEndElementAsync();
            }

            col += run.Text.Length;
        }

        // Render any remaining background at end of line
        if (lastBgColor != null)
        {
            await RenderBackgroundRectAsync(xml, row, bgStartCol, bgLength, lastBgColor);
        }
    }

    /// <summary>
    /// Renders a background rectangle for a range of characters.
    /// </summary>
    private async Task RenderBackgroundRectAsync(XmlWriter xml, int row, int startCol, int length, string color)
    {
        var x = startCol * _charWidth;
        // Align background with text visual bounds (text baseline is at (row + 1) * _charHeight)
        var y = (row + 1) * _charHeight - _charHeight * 0.85;
        var width = length * _charWidth;

        await xml.WriteStartElementAsync(null, "rect", null);
        await xml.WriteAttributeStringAsync(null, "x", null, FormatNumber(x));
        await xml.WriteAttributeStringAsync(null, "y", null, FormatNumber(y));
        await xml.WriteAttributeStringAsync(null, "width", null, FormatNumber(width));
        await xml.WriteAttributeStringAsync(null, "height", null, FormatNumber(_charHeight));
        await xml.WriteAttributeStringAsync(null, "fill", null, OptimizeHexColor(color));
        await xml.WriteEndElementAsync();
    }

    /// <summary>
    /// Builds style runs from a row of cells.
    /// </summary>
    private static List<StyleRun> BuildStyleRuns(TerminalCell[] cells, int row, int cursorY, int cursorX, bool isCursorIdle)
    {
        var runs = new List<StyleRun>();
        var currentRun = new StyleRun();

        for (var col = 0; col < cells.Length; col++)
        {
            var cell = cells[col];
            var isCursor = row == cursorY && col == cursorX;

            // Check if we need to start a new run
            var needNewRun = currentRun.Text.Length > 0 && (
                cell.ForegroundColor != currentRun.ForegroundColor ||
                cell.BackgroundColor != currentRun.BackgroundColor ||
                cell.IsBold != currentRun.IsBold ||
                cell.IsItalic != currentRun.IsItalic ||
                cell.IsUnderline != currentRun.IsUnderline ||
                isCursor != currentRun.IsCursor ||
                (isCursor && isCursorIdle != currentRun.IsCursorIdle)
            );

            if (needNewRun)
            {
                runs.Add(currentRun);
                currentRun = new StyleRun();
            }

            // Initialize or append
            if (currentRun.Text.Length == 0)
            {
                currentRun.ForegroundColor = cell.ForegroundColor;
                currentRun.BackgroundColor = cell.BackgroundColor;
                currentRun.IsBold = cell.IsBold;
                currentRun.IsItalic = cell.IsItalic;
                currentRun.IsUnderline = cell.IsUnderline;
                currentRun.IsCursor = isCursor;
                currentRun.IsCursorIdle = isCursor && isCursorIdle;
            }

            currentRun.Text += cell.Character;
        }

        if (currentRun.Text.Length > 0)
        {
            runs.Add(currentRun);
        }

        // Trim trailing spaces from last run
        if (runs.Count > 0)
        {
            runs[^1].Text = runs[^1].Text.TrimEnd();
        }

        // Remove trailing empty runs to reduce file size
        while (runs.Count > 0 && string.IsNullOrWhiteSpace(runs[^1].Text))
        {
            runs.RemoveAt(runs.Count - 1);
        }

        return runs;
    }

    /// <summary>
    /// Builds CSS class string for a style run.
    /// </summary>
    private static string BuildCssClasses(StyleRun run, bool disableCursor)
    {
        var classes = new List<string>();

        // Foreground color class (only for basic ANSI colors 0-15)
        // Extended colors (16-255) are handled as inline RGB in WriteTspanAsync
        if (run.ForegroundColor != null && IsAnsiColor(run.ForegroundColor))
        {
            if (int.TryParse(run.ForegroundColor, out var paletteIndex) && paletteIndex < 16)
            {
                classes.Add(GetAnsiColorClass(run.ForegroundColor));
            }
            // Extended palette colors will be rendered as inline fill attributes
        }
        else if (run.ForegroundColor == null)
        {
            classes.Add("fg");
        }

        // Style flags
        if (run.IsBold) classes.Add("bold");
        if (run.IsItalic) classes.Add("italic");
        if (run.IsUnderline) classes.Add("underline");

        // Cursor (only add class if cursor is not disabled)
        if (!disableCursor && run is { IsCursor: true, IsCursorIdle: true })
        {
            classes.Add("cursor-idle");
        }

        return string.Join(" ", classes);
    }

    /// <summary>
    /// Checks if a color is an ANSI palette color (0-255 index).
    /// </summary>
    private static bool IsAnsiColor(string color)
    {
        // Palette colors are passed as numeric strings ("0", "1", etc.)
        // RGB colors start with "#"
        return !color.StartsWith('#');
    }

    /// <summary>
    /// Gets the CSS class name for an ANSI palette color index.
    /// Maps palette indices 0-15 to VHS-style CSS classes.
    /// For extended colors (16-255), converts to RGB hex.
    /// </summary>
    private static string GetAnsiColorClass(string color)
    {
        // Try to parse as palette index
        if (!int.TryParse(color, out var paletteIndex))
        {
            return "fg"; // Default fallback
        }

        // Map standard ANSI colors (0-15) to CSS classes
        // VHS uses: k, r, g, y, b, m, c, w (normal) and K, R, G, Y, B, M, C, W (bright)
        return paletteIndex switch
        {
            0 => "k",  // Black
            1 => "r",  // Red
            2 => "g",  // Green
            3 => "y",  // Yellow
            4 => "b",  // Blue
            5 => "m",  // Magenta
            6 => "c",  // Cyan
            7 => "w",  // White
            8 => "K",  // Bright Black (Grey)
            9 => "R",  // Bright Red
            10 => "G", // Bright Green
            11 => "Y", // Bright Yellow
            12 => "B", // Bright Blue
            13 => "M", // Bright Magenta
            14 => "C", // Bright Cyan
            15 => "W", // Bright White
            // For extended palette (16-255), convert to RGB
            _ => "fg"
        };
    }

    /// <summary>
    /// Converts an xterm 256-color palette index to RGB hex color.
    /// Returns null if the index is out of range or is a basic color (0-15).
    /// </summary>
    private static string? PaletteIndexToRgb(int index)
    {
        if (index is < 16 or > 255) return null;

        // Colors 16-231: 216-color cube (6x6x6)
        if (index < 232)
        {
            var i = index - 16;
            var r = i / 36 * 51;
            var g = i / 6 % 6 * 51;
            var b = i % 6 * 51;
            return $"#{r:X2}{g:X2}{b:X2}";
        }

        // Colors 232-255: grayscale ramp
        var gray = 8 + (index - 232) * 10;
        return $"#{gray:X2}{gray:X2}{gray:X2}";
    }

    /// <summary>
    /// Formats a number efficiently - integers without decimals, fractional values with one decimal place.
    /// </summary>
    private static string FormatNumber(double value)
    {
        // If integer, skip decimals entirely to reduce file size
        if (Math.Abs(value % 1) < 0.001)
            return ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture);
        return value.ToString("F1", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Optimizes hex color codes by shortening them where possible (#ffffff → #fff).
    /// </summary>
    private static string OptimizeHexColor(string color)
    {
        // Check if it's a 7-character hex code where pairs are identical
        if (color.Length == 7 && color[0] == '#' &&
            color[1] == color[2] &&
            color[3] == color[4] &&
            color[5] == color[6])
        {
            return $"#{color[1]}{color[3]}{color[5]}";
        }
        return color;
    }

    /// <summary>
    /// Represents a run of characters with the same styling.
    /// </summary>
    private sealed class StyleRun
    {
        public string Text { get; set; } = "";
        public string? ForegroundColor { get; set; }
        public string? BackgroundColor { get; set; }
        public bool IsBold { get; set; }
        public bool IsItalic { get; set; }
        public bool IsUnderline { get; set; }
        public bool IsCursor { get; set; }
        public bool IsCursorIdle { get; set; }
    }
}

/// <summary>
/// Represents a terminal state with timestamp for animated SVG.
/// </summary>
public sealed class TerminalStateWithTime
{
    public required TerminalContent Content { get; init; }
    public required double TimestampSeconds { get; init; }
    public bool IsCursorIdle { get; init; }
}
