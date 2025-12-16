using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using VcrSharp.Core.Rendering;
using VcrSharp.Core.Session;

namespace VcrSharp.Infrastructure.Rendering;

/// <summary>
/// SVG renderer for terminal content. Supports both static screenshots and animated recordings.
/// For animations, uses SMIL visibility animations with partial updates:
/// 1. Diffs consecutive frames to find changed rows
/// 2. Renders each unique row state once
/// 3. Uses SMIL &lt;set&gt; elements to toggle visibility at appropriate times
/// 4. Animates cursor position separately
///
/// This can significantly reduce file size when only small portions of the screen change.
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

        // Styles
        await WriteStylesAsync(xml);

        // Clip path definition for terminal content area
        await xml.WriteStartElementAsync(null, "defs", null);
        await xml.WriteStartElementAsync(null, "clipPath", null);
        await xml.WriteAttributeStringAsync(null, "id", null, "terminal-clip");
        await xml.WriteStartElementAsync(null, "rect", null);
        await xml.WriteAttributeStringAsync(null, "width", null, _frameWidth.ToString(CultureInfo.InvariantCulture));
        await xml.WriteAttributeStringAsync(null, "height", null, _frameHeight.ToString(CultureInfo.InvariantCulture));
        await xml.WriteEndElementAsync(); // rect
        await xml.WriteEndElementAsync(); // clipPath
        await xml.WriteEndElementAsync(); // defs

        // Background (skip if transparent background is enabled)
        if (!_options.TransparentBackground)
        {
            await xml.WriteStartElementAsync(null, "rect", null);
            await xml.WriteAttributeStringAsync(null, "width", null, "100%");
            await xml.WriteAttributeStringAsync(null, "height", null, "100%");
            await xml.WriteAttributeStringAsync(null, "fill", null, OptimizeHexColor(_options.Theme.Background));
            await xml.WriteEndElementAsync();
        }

        // Terminal content group
        await xml.WriteStartElementAsync(null, "g", null);
        await xml.WriteAttributeStringAsync(null, "transform", null, $"translate({FormatNumber(_options.Padding)},{FormatNumber(_options.Padding)})");
        await xml.WriteAttributeStringAsync(null, "clip-path", null, "url(#terminal-clip)");

        // Content group with xml:space
        await xml.WriteStartElementAsync(null, "g", null);
        await xml.WriteAttributeStringAsync("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");

        // Render each line
        for (var row = 0; row < content.Rows; row++)
        {
            var y = row * _charHeight;
            await RenderRowContentAsync(xml, content.Cells[row], y);
        }

        await xml.WriteEndElementAsync(); // g (content)
        await xml.WriteEndElementAsync(); // g (terminal)
        await xml.WriteEndElementAsync(); // svg outer

        await xml.FlushAsync();
    }

    /// <summary>
    /// Renders multiple terminal states as an animated SVG using SMIL animations.
    /// </summary>
    public async Task RenderAnimatedAsync(
        string outputPath,
        IReadOnlyList<TerminalStateWithTime> states,
        double totalDurationSeconds,
        CancellationToken cancellationToken = default)
    {
        if (states.Count == 0)
            throw new ArgumentException("No states to render", nameof(states));

        // Ensure output directory exists
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Build the row timeline - tracks when each unique row content appears/disappears
        var rowTimeline = BuildRowTimeline(states, totalDurationSeconds);

        // Build cursor timeline
        var cursorTimeline = BuildCursorTimeline(states, totalDurationSeconds);

        await using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);
        await using var xml = XmlWriter.Create(writer, new XmlWriterSettings
        {
            Async = true,
            Indent = false,
            OmitXmlDeclaration = true,
            Encoding = Encoding.UTF8
        });

        // Outer SVG
        await xml.WriteStartElementAsync(null, "svg", "http://www.w3.org/2000/svg");
        await xml.WriteAttributeStringAsync(null, "viewBox", null, $"0 0 {_options.Width} {_options.Height}");

        // Styles (minimal - no keyframe animations needed)
        await WriteStylesAsync(xml);

        // Clip path definition for terminal content area (Safari has issues with nested SVGs)
        await xml.WriteStartElementAsync(null, "defs", null);
        await xml.WriteStartElementAsync(null, "clipPath", null);
        await xml.WriteAttributeStringAsync(null, "id", null, "terminal-clip");
        await xml.WriteStartElementAsync(null, "rect", null);
        await xml.WriteAttributeStringAsync(null, "width", null, _frameWidth.ToString(CultureInfo.InvariantCulture));
        await xml.WriteAttributeStringAsync(null, "height", null, _frameHeight.ToString(CultureInfo.InvariantCulture));
        await xml.WriteEndElementAsync(); // rect
        await xml.WriteEndElementAsync(); // clipPath
        await xml.WriteEndElementAsync(); // defs

        // Background
        if (!_options.TransparentBackground)
        {
            await xml.WriteStartElementAsync(null, "rect", null);
            await xml.WriteAttributeStringAsync(null, "width", null, "100%");
            await xml.WriteAttributeStringAsync(null, "height", null, "100%");
            await xml.WriteAttributeStringAsync(null, "fill", null, OptimizeHexColor(_options.Theme.Background));
            await xml.WriteEndElementAsync();
        }

        // Terminal content group (replaces nested SVG for better Safari compatibility)
        await xml.WriteStartElementAsync(null, "g", null);
        await xml.WriteAttributeStringAsync(null, "transform", null, $"translate({FormatNumber(_options.Padding)},{FormatNumber(_options.Padding)})");
        await xml.WriteAttributeStringAsync(null, "clip-path", null, "url(#terminal-clip)");

        // Content group
        await xml.WriteStartElementAsync(null, "g", null);
        await xml.WriteAttributeStringAsync("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");

        // Render each unique row state with SMIL visibility animations
        await RenderRowsWithSmilAsync(xml, rowTimeline, totalDurationSeconds);

        // Render cursor with SMIL position animation
        if (!_options.DisableCursor)
        {
            await RenderCursorWithSmilAsync(xml, cursorTimeline, totalDurationSeconds);
        }

        await xml.WriteEndElementAsync(); // g (content)
        await xml.WriteEndElementAsync(); // g (terminal)
        await xml.WriteEndElementAsync(); // svg outer

        await xml.FlushAsync();
    }

    /// <summary>
    /// Builds a timeline of row states - when each unique row content appears and disappears.
    /// </summary>
    private RowTimeline BuildRowTimeline(IReadOnlyList<TerminalStateWithTime> states, double totalDuration)
    {
        var timeline = new RowTimeline();
        var rows = states[0].Content.Rows;

        for (var row = 0; row < rows; row++)
        {
            var rowStates = new List<RowStateInterval>();
            string? currentHash = null;
            TerminalCell[]? currentCells = null;
            double intervalStart = 0;

            for (var i = 0; i < states.Count; i++)
            {
                var state = states[i];
                var cells = state.Content.Cells[row];
                var hash = ComputeRowHash(cells);
                var timestamp = state.TimestampSeconds;

                if (hash != currentHash)
                {
                    // Row content changed - close previous interval if any
                    if (currentHash != null && currentCells != null)
                    {
                        rowStates.Add(new RowStateInterval
                        {
                            Cells = currentCells,
                            Hash = currentHash,
                            StartTime = intervalStart,
                            EndTime = timestamp
                        });
                    }

                    // Start new interval
                    currentHash = hash;
                    currentCells = cells;
                    intervalStart = timestamp;
                }
            }

            // Close final interval
            if (currentHash != null && currentCells != null)
            {
                rowStates.Add(new RowStateInterval
                {
                    Cells = currentCells,
                    Hash = currentHash,
                    StartTime = intervalStart,
                    EndTime = totalDuration
                });
            }

            timeline.Rows[row] = rowStates;
        }

        return timeline;
    }

    /// <summary>
    /// Builds a timeline of cursor positions.
    /// </summary>
    private List<CursorKeyframe> BuildCursorTimeline(IReadOnlyList<TerminalStateWithTime> states, double totalDuration)
    {
        var keyframes = new List<CursorKeyframe>();
        int? lastX = null;
        int? lastY = null;
        bool? lastVisible = null;

        foreach (var state in states)
        {
            var content = state.Content;

            // Only add keyframe if position or visibility changed
            if (content.CursorX != lastX || content.CursorY != lastY || content.CursorVisible != lastVisible)
            {
                keyframes.Add(new CursorKeyframe
                {
                    X = content.CursorX,
                    Y = content.CursorY,
                    Visible = content.CursorVisible,
                    Timestamp = state.TimestampSeconds
                });

                lastX = content.CursorX;
                lastY = content.CursorY;
                lastVisible = content.CursorVisible;
            }
        }

        return keyframes;
    }

    /// <summary>
    /// Renders rows using SMIL visibility animations.
    /// Uses &lt;animate&gt; with keyTimes for proper looping instead of &lt;set&gt; with dur.
    /// </summary>
    private async Task RenderRowsWithSmilAsync(XmlWriter xml, RowTimeline timeline, double totalDuration)
    {
        foreach (var (row, intervals) in timeline.Rows)
        {
            var y = row * _charHeight;

            // Group unique row states by hash to avoid rendering duplicates
            var uniqueStates = new Dictionary<string, (TerminalCell[] Cells, List<(double Start, double End)> Intervals)>();

            foreach (var interval in intervals)
            {
                if (!uniqueStates.TryGetValue(interval.Hash, out var existing))
                {
                    existing = (interval.Cells, new List<(double, double)>());
                    uniqueStates[interval.Hash] = existing;
                }
                existing.Intervals.Add((interval.StartTime, interval.EndTime));
            }

            // Render each unique row state with visibility animations
            foreach (var (hash, (cells, intervalList)) in uniqueStates)
            {
                // Skip empty rows
                var rowText = string.Concat(cells.Select(c => c.Character)).TrimEnd();
                if (string.IsNullOrWhiteSpace(rowText) && !cells.Any(c => c.BackgroundColor != null))
                    continue;

                // Determine if this state needs animation or is always visible
                var isAlwaysVisible = intervalList.Count == 1 &&
                                      Math.Abs(intervalList[0].Start) < 0.001 &&
                                      Math.Abs(intervalList[0].End - totalDuration) < 0.001;

                await xml.WriteStartElementAsync(null, "g", null);

                if (!isAlwaysVisible)
                {
                    // Build keyTimes animation for looping visibility
                    // This uses <animate> instead of <set> so it properly loops
                    var (values, keyTimes) = BuildVisibilityAnimation(intervalList, totalDuration);

                    await xml.WriteStartElementAsync(null, "animate", null);
                    await xml.WriteAttributeStringAsync(null, "attributeName", null, "visibility");
                    await xml.WriteAttributeStringAsync(null, "values", null, values);
                    await xml.WriteAttributeStringAsync(null, "keyTimes", null, keyTimes);
                    await xml.WriteAttributeStringAsync(null, "dur", null, $"{FormatTime(totalDuration)}s");
                    await xml.WriteAttributeStringAsync(null, "calcMode", null, "discrete");
                    await xml.WriteAttributeStringAsync(null, "repeatCount", null, "indefinite");
                    await xml.WriteEndElementAsync();
                }

                // Render the row content
                await RenderRowContentAsync(xml, cells, y);

                await xml.WriteEndElementAsync(); // g
            }
        }
    }

    /// <summary>
    /// Builds visibility animation values and keyTimes for a set of intervals.
    /// Creates a "hidden;visible;hidden;visible;..." pattern with corresponding keyTimes.
    /// </summary>
    private static (string Values, string KeyTimes) BuildVisibilityAnimation(
        List<(double Start, double End)> intervals,
        double totalDuration)
    {
        // Sort intervals by start time
        var sorted = intervals.OrderBy(i => i.Start).ToList();

        var values = new List<string>();
        var keyTimes = new List<double>();

        // Start hidden at time 0 (unless first interval starts at 0)
        if (sorted[0].Start > 0.001)
        {
            values.Add("hidden");
            keyTimes.Add(0);
        }

        foreach (var (start, end) in sorted)
        {
            // Visible at start
            values.Add("visible");
            keyTimes.Add(start / totalDuration);

            // Hidden at end (unless it extends to the total duration)
            if (end < totalDuration - 0.001)
            {
                values.Add("hidden");
                keyTimes.Add(end / totalDuration);
            }
        }

        // Safari requires keyTimes to explicitly end at 1.0 for proper looping.
        // Add a final keyframe at 1.0 with the same value as the last keyframe.
        if (keyTimes.Count > 0 && keyTimes[^1] < 0.999)
        {
            values.Add(values[^1]); // Repeat last visibility state
            keyTimes.Add(1.0);
        }

        // Format output
        var valuesStr = string.Join(";", values);
        var keyTimesStr = string.Join(";", keyTimes.Select(kt => FormatTime(Math.Min(kt, 1.0))));

        return (valuesStr, keyTimesStr);
    }

    /// <summary>
    /// Renders a single row's content (backgrounds and text).
    /// Uses cell widths for proper positioning with wide characters.
    /// </summary>
    private async Task RenderRowContentAsync(XmlWriter xml, TerminalCell[] cells, double y)
    {
        // Build style runs
        var runs = BuildStyleRuns(cells);
        if (runs.Count == 0) return;

        // Render backgrounds using cell widths for positioning
        var cellCol = 0;
        foreach (var run in runs)
        {
            if (run.BackgroundColor != null)
            {
                var x = cellCol * _charWidth;
                var width = run.CellWidth * _charWidth;

                await xml.WriteStartElementAsync(null, "rect", null);
                await xml.WriteAttributeStringAsync(null, "x", null, FormatNumber(x));
                await xml.WriteAttributeStringAsync(null, "y", null, FormatNumber(y));
                await xml.WriteAttributeStringAsync(null, "width", null, FormatNumber(width));
                await xml.WriteAttributeStringAsync(null, "height", null, FormatNumber(_charHeight));
                await xml.WriteAttributeStringAsync(null, "fill", null, ConvertColorToHex(run.BackgroundColor));
                await xml.WriteEndElementAsync();
            }
            cellCol += run.CellWidth;
        }

        // Render each run as a separate <text> element with explicit positioning and textLength.
        // Using <text> instead of <tspan> because Firefox supports textLength on <text> but not <tspan>.
        // y offset (0.9em) is pre-calculated for cross-browser baseline handling since Safari
        // doesn't properly support dominant-baseline:hanging and clips text at y=0
        var yWithBaseline = y + _options.FontSize * 0.9;
        var cumulativeCellWidth = 0;
        foreach (var run in runs)
        {
            var x = cumulativeCellWidth * _charWidth;
            var runLength = run.CellWidth * _charWidth;

            await xml.WriteStartElementAsync(null, "text", null);
            await xml.WriteAttributeStringAsync(null, "x", null, FormatNumber(x));
            await xml.WriteAttributeStringAsync(null, "y", null, FormatNumber(yWithBaseline));
            await xml.WriteAttributeStringAsync(null, "textLength", null, FormatNumber(runLength));

            // Box-drawing and block characters need spacingAndGlyphs to connect seamlessly.
            // Regular text is fine with default spacing adjustment.
            if (run.Text.Any(c => (c >= 0x2500 && c <= 0x257F) || (c >= 0x2580 && c <= 0x259F)))
            {
                await xml.WriteAttributeStringAsync(null, "lengthAdjust", null, "spacingAndGlyphs");
            }

            var classes = BuildCssClasses(run);
            if (!string.IsNullOrEmpty(classes))
            {
                await xml.WriteAttributeStringAsync(null, "class", null, classes);
            }

            // Inline color for RGB colors
            if (run.ForegroundColor != null && run.ForegroundColor.StartsWith('#'))
            {
                await xml.WriteAttributeStringAsync(null, "fill", null, OptimizeHexColor(run.ForegroundColor));
            }
            else if (run.ForegroundColor != null && int.TryParse(run.ForegroundColor, out var idx) && idx >= 16)
            {
                var rgb = PaletteIndexToRgb(idx);
                if (rgb != null)
                    await xml.WriteAttributeStringAsync(null, "fill", null, rgb);
            }

            await xml.WriteStringAsync(run.Text);
            await xml.WriteEndElementAsync(); // text

            cumulativeCellWidth += run.CellWidth;
        }
    }

    /// <summary>
    /// Renders cursor with SMIL position animation.
    /// </summary>
    private async Task RenderCursorWithSmilAsync(XmlWriter xml, List<CursorKeyframe> keyframes, double totalDuration)
    {
        if (keyframes.Count == 0) return;

        var first = keyframes[0];
        var x = first.X * _charWidth;
        var y = first.Y * _charHeight;

        await xml.WriteStartElementAsync(null, "rect", null);
        await xml.WriteAttributeStringAsync(null, "class", null, "cursor-block");
        await xml.WriteAttributeStringAsync(null, "width", null, FormatNumber(_charWidth));
        await xml.WriteAttributeStringAsync(null, "height", null, FormatNumber(_charHeight));
        await xml.WriteAttributeStringAsync(null, "x", null, FormatNumber(x));
        await xml.WriteAttributeStringAsync(null, "y", null, FormatNumber(y));

        // X position animation
        if (keyframes.Count > 1)
        {
            // Build keyTimes list, ensuring it ends at 1.0 for Safari looping compatibility
            var keyTimesList = keyframes.Select(k => k.Timestamp / totalDuration).ToList();
            var xValuesList = keyframes.Select(k => FormatNumber(k.X * _charWidth)).ToList();
            var yValuesList = keyframes.Select(k => FormatNumber(k.Y * _charHeight)).ToList();

            // Safari requires keyTimes to explicitly end at 1.0 for proper looping
            if (keyTimesList[^1] < 0.999)
            {
                keyTimesList.Add(1.0);
                xValuesList.Add(xValuesList[^1]); // Repeat last position
                yValuesList.Add(yValuesList[^1]);
            }

            var keyTimes = string.Join(";", keyTimesList.Select(FormatTime));
            var xValues = string.Join(";", xValuesList);
            var yValues = string.Join(";", yValuesList);

            await xml.WriteStartElementAsync(null, "animate", null);
            await xml.WriteAttributeStringAsync(null, "attributeName", null, "x");
            await xml.WriteAttributeStringAsync(null, "values", null, xValues);
            await xml.WriteAttributeStringAsync(null, "keyTimes", null, keyTimes);
            await xml.WriteAttributeStringAsync(null, "dur", null, $"{FormatTime(totalDuration)}s");
            await xml.WriteAttributeStringAsync(null, "calcMode", null, "discrete");
            await xml.WriteAttributeStringAsync(null, "repeatCount", null, "indefinite");
            await xml.WriteEndElementAsync();

            // Y position animation
            await xml.WriteStartElementAsync(null, "animate", null);
            await xml.WriteAttributeStringAsync(null, "attributeName", null, "y");
            await xml.WriteAttributeStringAsync(null, "values", null, yValues);
            await xml.WriteAttributeStringAsync(null, "keyTimes", null, keyTimes);
            await xml.WriteAttributeStringAsync(null, "dur", null, $"{FormatTime(totalDuration)}s");
            await xml.WriteAttributeStringAsync(null, "calcMode", null, "discrete");
            await xml.WriteAttributeStringAsync(null, "repeatCount", null, "indefinite");
            await xml.WriteEndElementAsync();
        }

        // Cursor blink animation
        await xml.WriteStartElementAsync(null, "animate", null);
        await xml.WriteAttributeStringAsync(null, "attributeName", null, "opacity");
        await xml.WriteAttributeStringAsync(null, "values", null, "1;0;1");
        await xml.WriteAttributeStringAsync(null, "keyTimes", null, "0;0.5;1");
        await xml.WriteAttributeStringAsync(null, "dur", null, "1s");
        await xml.WriteAttributeStringAsync(null, "repeatCount", null, "indefinite");
        await xml.WriteEndElementAsync();

        await xml.WriteEndElementAsync(); // rect
    }

    /// <summary>
    /// Writes CSS styles (minimal - SMIL handles animations).
    /// </summary>
    private async Task WriteStylesAsync(XmlWriter xml)
    {
        await xml.WriteStartElementAsync(null, "style", null);

        var css = new StringBuilder();

        // Base text styles (baseline is pre-calculated into y position for cross-browser compatibility)
        css.Append($"text{{white-space:pre;font-family:{_options.FontFamily};font-size:{_options.FontSize}px;letter-spacing:0;word-spacing:0;text-rendering:geometricPrecision;font-variant-ligatures:none}}");
        css.Append($".fg{{fill:{OptimizeHexColor(_options.Theme.Foreground)}}}");

        // ANSI color classes
        AppendAnsiColorStyles(css);

        // Style flags
        css.Append(".bold{font-weight:bold}.italic{font-style:italic}.underline{text-decoration:underline}");

        // Cursor
        if (!_options.DisableCursor)
        {
            css.Append($".cursor-block{{fill:{OptimizeHexColor(_options.Theme.Foreground)}}}");
        }

        await xml.WriteStringAsync(css.ToString());
        await xml.WriteEndElementAsync();
    }

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

    private void CalculateDimensions()
    {
        _charWidth = _options.ActualCellWidth ?? _options.FontSize * 0.55;
        _charHeight = _options.ActualCellHeight ?? _options.FontSize * 1.2;
        _frameWidth = _options.Width - 2 * _options.Padding;
        _frameHeight = _options.Height - 2 * _options.Padding;
    }

    private static string ComputeRowHash(TerminalCell[] cells)
    {
        var sb = new StringBuilder();
        foreach (var cell in cells)
        {
            sb.Append(cell.Character);
            sb.Append(cell.ForegroundColor ?? "");
            sb.Append(cell.BackgroundColor ?? "");
            sb.Append(cell.IsBold ? "b" : "");
            sb.Append(cell.IsItalic ? "i" : "");
            sb.Append(cell.IsUnderline ? "u" : "");
        }
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = MD5.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Builds style runs from a row of cells.
    /// Skips continuation cells (width 0) which are placeholders after wide characters.
    /// Tracks cell width for proper SVG text positioning.
    /// </summary>
    private static List<StyleRun> BuildStyleRuns(TerminalCell[] cells)
    {
        var runs = new List<StyleRun>();
        var currentRun = new StyleRun();

        foreach (var cell in cells)
        {
            // Skip continuation cells (width 0) - these are placeholders after wide characters
            if (cell.Width == 0)
                continue;

            var needNewRun = currentRun.Text.Length > 0 && (
                cell.ForegroundColor != currentRun.ForegroundColor ||
                cell.BackgroundColor != currentRun.BackgroundColor ||
                cell.IsBold != currentRun.IsBold ||
                cell.IsItalic != currentRun.IsItalic ||
                cell.IsUnderline != currentRun.IsUnderline
            );

            if (needNewRun)
            {
                runs.Add(currentRun);
                currentRun = new StyleRun();
            }

            if (currentRun.Text.Length == 0)
            {
                currentRun.ForegroundColor = cell.ForegroundColor;
                currentRun.BackgroundColor = cell.BackgroundColor;
                currentRun.IsBold = cell.IsBold;
                currentRun.IsItalic = cell.IsItalic;
                currentRun.IsUnderline = cell.IsUnderline;
            }

            currentRun.Text += cell.Character;
            currentRun.CellWidth += cell.Width; // Track actual cell width (1 or 2)
        }

        if (currentRun.Text.Length > 0)
        {
            runs.Add(currentRun);
        }

        // Trim trailing spaces (and adjust cell width accordingly)
        if (runs.Count > 0)
        {
            var lastRun = runs[^1];
            var originalLength = lastRun.Text.Length;
            lastRun.Text = lastRun.Text.TrimEnd();
            // Spaces are always width 1, so subtract the difference
            lastRun.CellWidth -= (originalLength - lastRun.Text.Length);
        }

        while (runs.Count > 0 && string.IsNullOrWhiteSpace(runs[^1].Text))
        {
            runs.RemoveAt(runs.Count - 1);
        }

        return runs;
    }

    private static string BuildCssClasses(StyleRun run)
    {
        var classes = new List<string>();

        if (run.ForegroundColor != null && !run.ForegroundColor.StartsWith('#'))
        {
            if (int.TryParse(run.ForegroundColor, out var idx) && idx < 16)
            {
                classes.Add(GetAnsiColorClass(idx));
            }
        }
        else if (run.ForegroundColor == null)
        {
            classes.Add("fg");
        }

        if (run.IsBold) classes.Add("bold");
        if (run.IsItalic) classes.Add("italic");
        if (run.IsUnderline) classes.Add("underline");

        return string.Join(" ", classes);
    }

    private static string GetAnsiColorClass(int index) => index switch
    {
        0 => "k", 1 => "r", 2 => "g", 3 => "y",
        4 => "b", 5 => "m", 6 => "c", 7 => "w",
        8 => "K", 9 => "R", 10 => "G", 11 => "Y",
        12 => "B", 13 => "M", 14 => "C", 15 => "W",
        _ => "fg"
    };

    private static string? PaletteIndexToRgb(int index)
    {
        if (index is < 16 or > 255) return null;

        if (index < 232)
        {
            var i = index - 16;
            var r = i / 36 * 51;
            var g = i / 6 % 6 * 51;
            var b = i % 6 * 51;
            return $"#{r:X2}{g:X2}{b:X2}";
        }

        var gray = 8 + (index - 232) * 10;
        return $"#{gray:X2}{gray:X2}{gray:X2}";
    }

    private static string FormatNumber(double value)
    {
        if (Math.Abs(value % 1) < 0.001)
            return ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture);
        return value.ToString("F1", CultureInfo.InvariantCulture);
    }

    private static string FormatTime(double seconds)
    {
        return seconds.ToString("F3", CultureInfo.InvariantCulture);
    }

    private static string OptimizeHexColor(string color)
    {
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
    /// Converts any color format (hex or palette index) to a hex color string.
    /// Handles RGB hex colors, basic ANSI colors (0-15), and extended palette (16-255).
    /// </summary>
    private string ConvertColorToHex(string color)
    {
        // Already a hex color
        if (color.StartsWith('#'))
        {
            return OptimizeHexColor(color);
        }

        // Palette index
        if (int.TryParse(color, out var paletteIndex))
        {
            // Basic ANSI colors (0-15) - use theme colors
            var themeColor = paletteIndex switch
            {
                0 => _options.Theme.Black,
                1 => _options.Theme.Red,
                2 => _options.Theme.Green,
                3 => _options.Theme.Yellow,
                4 => _options.Theme.Blue,
                5 => _options.Theme.Magenta,
                6 => _options.Theme.Cyan,
                7 => _options.Theme.White,
                8 => _options.Theme.BrightBlack,
                9 => _options.Theme.BrightRed,
                10 => _options.Theme.BrightGreen,
                11 => _options.Theme.BrightYellow,
                12 => _options.Theme.BrightBlue,
                13 => _options.Theme.BrightMagenta,
                14 => _options.Theme.BrightCyan,
                15 => _options.Theme.BrightWhite,
                _ => null
            };

            if (themeColor != null)
            {
                return OptimizeHexColor(themeColor);
            }

            // Extended palette (16-255)
            var rgb = PaletteIndexToRgb(paletteIndex);
            if (rgb != null)
            {
                return rgb;
            }
        }

        // Fallback - return as-is (shouldn't happen)
        return color;
    }

    // Internal data structures

    private sealed class RowTimeline
    {
        public Dictionary<int, List<RowStateInterval>> Rows { get; } = new();
    }

    private sealed class RowStateInterval
    {
        public required TerminalCell[] Cells { get; init; }
        public required string Hash { get; init; }
        public required double StartTime { get; init; }
        public required double EndTime { get; init; }
    }

    private sealed class CursorKeyframe
    {
        public int X { get; init; }
        public int Y { get; init; }
        public bool Visible { get; init; }
        public double Timestamp { get; init; }
    }

    private sealed class StyleRun
    {
        public string Text { get; set; } = "";
        public string? ForegroundColor { get; set; }
        public string? BackgroundColor { get; set; }
        public bool IsBold { get; set; }
        public bool IsItalic { get; set; }
        public bool IsUnderline { get; set; }
        /// <summary>
        /// Total cell width of this run (accounts for wide characters taking 2 cells).
        /// </summary>
        public int CellWidth { get; set; }
    }
}
