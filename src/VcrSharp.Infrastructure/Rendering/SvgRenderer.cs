using System.Globalization;
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

    // Canvas (root viewBox/intrinsic) size in pixels. Equals Width/Height unless
    // fit-to-content cropping overrides them via SetContentExtent.
    private int _canvasWidth;
    private int _canvasHeight;

    // Content extent override (character cells) when FitToContent is enabled.
    private int? _cropCols;
    private int? _cropRows;

    // Measured content extent (character cells) used to GROW the canvas so no rendered cell is
    // clipped when the configured viewport is smaller than the content actually captured.
    // Distinct from _cropCols/_cropRows, which SHRINK the canvas for FitToContent.
    private int? _measuredExtentCols;
    private int? _measuredExtentRows;

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

        // Grow the canvas if the captured content is wider/taller than the configured viewport
        // so trailing columns/rows (e.g. a table's right border) are never clipped.
        EnsureCanvasFitsContent(ContentExtent.Measure(content));

        await using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);
        await using var xml = XmlWriter.Create(writer, new XmlWriterSettings
        {
            Async = true,
            Indent = false,
            OmitXmlDeclaration = true,
            Encoding = Encoding.UTF8
        });

        // Outer SVG with viewBox (+ optional intrinsic size and metadata) for responsive scaling
        await xml.WriteStartElementAsync(null, "svg", "http://www.w3.org/2000/svg");
        await WriteRootSvgAttributesAsync(xml, content.Cols, content.Rows);

        // Styles
        await WriteStylesAsync(xml);

        // Defs section with clip path and shade patterns
        await xml.WriteStartElementAsync(null, "defs", null);

        // Clip path for terminal content area
        await xml.WriteStartElementAsync(null, "clipPath", null);
        await xml.WriteAttributeStringAsync(null, "id", null, "terminal-clip");
        await xml.WriteStartElementAsync(null, "rect", null);
        await xml.WriteAttributeStringAsync(null, "width", null, _frameWidth.ToString(CultureInfo.InvariantCulture));
        await xml.WriteAttributeStringAsync(null, "height", null, _frameHeight.ToString(CultureInfo.InvariantCulture));
        await xml.WriteEndElementAsync(); // rect
        await xml.WriteEndElementAsync(); // clipPath

        // Shade patterns for block elements
        await WriteShadePatternDefsAsync(xml);

        await xml.WriteEndElementAsync(); // defs

        // Background (skip if transparent background is enabled)
        if (!_options.TransparentBackground)
        {
            await xml.WriteStartElementAsync(null, "rect", null);
            await xml.WriteAttributeStringAsync(null, "width", null, "100%");
            await xml.WriteAttributeStringAsync(null, "height", null, "100%");
            await xml.WriteAttributeStringAsync(null, "fill", null, BgFill(_options.Theme.Background));
            await xml.WriteEndElementAsync();
        }

        // Terminal content group
        await xml.WriteStartElementAsync(null, "g", null);
        await xml.WriteAttributeStringAsync(null, "transform", null, $"translate({FormatNumber(_options.Padding)},{FormatNumber(_options.Padding)})");
        // In fit-to-content mode the canvas is already sized to content, so the clip-path
        // (which can shave the last row's descenders) is intentionally omitted.
        if (!FitMode)
            await xml.WriteAttributeStringAsync(null, "clip-path", null, "url(#terminal-clip)");

        // Content group with xml:space
        await xml.WriteStartElementAsync(null, "g", null);
        await xml.WriteAttributeStringAsync("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");

        // Render each line (capped to the cropped extent when fit-to-content is active)
        var rowCount = _cropRows.HasValue ? Math.Min(content.Rows, _cropRows.Value) : content.Rows;
        for (var row = 0; row < rowCount; row++)
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

        // Grow the canvas if the captured content is wider/taller than the configured viewport
        // so trailing columns/rows (e.g. a table's right border) are never clipped. Uses the
        // union across all frames so a column that only appears partway through is still covered.
        EnsureCanvasFitsContent(ContentExtent.Union(states.Select(s => s.Content)));

        // Build the row timeline - tracks when each unique row content appears/disappears
        var rowTimeline = BuildRowTimeline(states, totalDurationSeconds);

        // Build cursor timeline
        var cursorTimeline = BuildCursorTimeline(states);

        await using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);
        await using var xml = XmlWriter.Create(writer, new XmlWriterSettings
        {
            Async = true,
            Indent = false,
            OmitXmlDeclaration = true,
            Encoding = Encoding.UTF8
        });

        // Outer SVG (viewBox + optional intrinsic size and metadata)
        await xml.WriteStartElementAsync(null, "svg", "http://www.w3.org/2000/svg");
        await WriteRootSvgAttributesAsync(xml, states[0].Content.Cols, states[0].Content.Rows);

        // Styles (minimal - no keyframe animations needed)
        await WriteStylesAsync(xml);

        // Defs section with clip path and shade patterns (Safari has issues with nested SVGs)
        await xml.WriteStartElementAsync(null, "defs", null);

        // Clip path for terminal content area
        await xml.WriteStartElementAsync(null, "clipPath", null);
        await xml.WriteAttributeStringAsync(null, "id", null, "terminal-clip");
        await xml.WriteStartElementAsync(null, "rect", null);
        await xml.WriteAttributeStringAsync(null, "width", null, _frameWidth.ToString(CultureInfo.InvariantCulture));
        await xml.WriteAttributeStringAsync(null, "height", null, _frameHeight.ToString(CultureInfo.InvariantCulture));
        await xml.WriteEndElementAsync(); // rect
        await xml.WriteEndElementAsync(); // clipPath

        // Shade patterns for block elements
        await WriteShadePatternDefsAsync(xml);

        await xml.WriteEndElementAsync(); // defs

        // Background
        if (!_options.TransparentBackground)
        {
            await xml.WriteStartElementAsync(null, "rect", null);
            await xml.WriteAttributeStringAsync(null, "width", null, "100%");
            await xml.WriteAttributeStringAsync(null, "height", null, "100%");
            await xml.WriteAttributeStringAsync(null, "fill", null, BgFill(_options.Theme.Background));
            await xml.WriteEndElementAsync();
        }

        // Terminal content group (replaces nested SVG for better Safari compatibility)
        await xml.WriteStartElementAsync(null, "g", null);
        await xml.WriteAttributeStringAsync(null, "transform", null, $"translate({FormatNumber(_options.Padding)},{FormatNumber(_options.Padding)})");
        // In fit-to-content mode the canvas is already sized to content, so the clip-path
        // (which can shave the last row's descenders) is intentionally omitted.
        if (!FitMode)
            await xml.WriteAttributeStringAsync(null, "clip-path", null, "url(#terminal-clip)");

        // Content group
        await xml.WriteStartElementAsync(null, "g", null);
        await xml.WriteAttributeStringAsync("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");

        // Render each unique row state with SMIL visibility animations
        await RenderRowsWithSmilAsync(xml, rowTimeline, totalDurationSeconds);

        // Render cursor with SMIL position animation (skip if cursor is disabled or not blinking)
        if (!_options.DisableCursor && _options.CursorBlink)
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
            ulong? currentHash = null;
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
                            Hash = currentHash.Value,
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
                    Hash = currentHash.Value,
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
    private List<CursorKeyframe> BuildCursorTimeline(IReadOnlyList<TerminalStateWithTime> states)
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
            var uniqueStates = new Dictionary<ulong, (TerminalCell[] Cells, List<(double Start, double End)> Intervals)>();

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
            foreach (var (_, (cells, intervalList)) in uniqueStates)
            {
                // Skip empty rows (shared blank-cell definition)
                if (ContentAnalysis.IsBlankRow(cells))
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
                    await WriteRepeatCountAsync(xml, freezeEligible: true);
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
    /// Custom glyphs (box drawing, block elements, powerline) are rendered as SVG paths
    /// instead of text for pixel-perfect alignment.
    /// </summary>
    private async Task RenderRowContentAsync(XmlWriter xml, TerminalCell[] cells, double y)
    {
        // Build style runs, splitting at custom glyph boundaries
        var segments = BuildRenderSegments(cells);
        if (segments.Count == 0) return;

        // Calculate stroke widths for box-drawing characters
        var lightStroke = Math.Max(1, _charHeight * 0.08);
        var heavyStroke = Math.Max(2, _charHeight * 0.16);

        // Render backgrounds for text segments (custom glyph backgrounds are handled in RenderGlyph)
        var cellCol = 0;
        foreach (var segment in segments)
        {
            var backgroundFill = ResolveBackgroundFill(segment);
            if (!segment.IsCustomGlyph && backgroundFill != null)
            {
                var x = cellCol * _charWidth;
                var width = segment.CellWidth * _charWidth;

                await xml.WriteStartElementAsync(null, "rect", null);
                await xml.WriteAttributeStringAsync(null, "x", null, FormatNumber(x));
                await xml.WriteAttributeStringAsync(null, "y", null, FormatNumber(y));
                await xml.WriteAttributeStringAsync(null, "width", null, FormatNumber(width));
                await xml.WriteAttributeStringAsync(null, "height", null, FormatNumber(_charHeight));
                await xml.WriteAttributeStringAsync(null, "fill", null, backgroundFill);
                await xml.WriteEndElementAsync();
            }
            cellCol += segment.CellWidth;
        }

        // Render each segment
        var yWithBaseline = y + _options.FontSize * 0.9;
        var cumulativeCellWidth = 0;
        foreach (var segment in segments)
        {
            var x = cumulativeCellWidth * _charWidth;

            // Conceal (SGR 8): keep the background (already painted above) but draw no glyph, so the
            // cell renders blank. Reverse+conceal therefore yields a solid block — matching raster.
            if (segment.IsConceal)
            {
                cumulativeCellWidth += segment.CellWidth;
                continue;
            }

            if (segment.IsCustomGlyph)
            {
                // A segment is a single style run, so its colors are constant — resolve them once.
                var fgColor = GetForegroundColorHex(segment);
                var bgColor = segment.BackgroundColor != null ? ConvertColorToHex(segment.BackgroundColor) : null;

                // Collapse a run of the SAME horizontally-tileable glyph (a solid '─'/'━'/'═' rule or a
                // full-width '█'/half-band/shade) into ONE element spanning the run instead of one path
                // per cell. Runs only form within a style segment, so a per-cell gradient is segments of
                // length 1 and falls straight through to the unchanged per-glyph path below.
                var text = segment.Text;
                var glyphX = x;
                var i = 0;
                while (i < text.Length)
                {
                    var ch = text[i];
                    var runLen = 1;
                    while (i + runLen < text.Length && text[i + runLen] == ch) runLen++;

                    if (runLen > 1 &&
                        CustomGlyphRenderer.TryRenderHorizontalRun(
                            ch, glyphX, y, _charWidth, _charHeight, runLen,
                            fgColor, bgColor, lightStroke, heavyStroke, out var runSvg))
                    {
                        await xml.WriteRawAsync(runSvg);
                    }
                    else
                    {
                        for (var k = 0; k < runLen; k++)
                        {
                            var glyphSvg = CustomGlyphRenderer.RenderGlyph(
                                ch, glyphX + k * _charWidth, y, _charWidth, _charHeight,
                                fgColor, bgColor, lightStroke, heavyStroke);
                            if (glyphSvg != null)
                                await xml.WriteRawAsync(glyphSvg);
                        }
                    }

                    glyphX += runLen * _charWidth; // All custom glyphs are single-width
                    i += runLen;
                }
            }
            else
            {
                // Render text using existing approach
                var runLength = segment.CellWidth * _charWidth;

                await xml.WriteStartElementAsync(null, "text", null);
                await xml.WriteAttributeStringAsync(null, "x", null, FormatNumber(x));
                await xml.WriteAttributeStringAsync(null, "y", null, FormatNumber(yWithBaseline));
                await xml.WriteAttributeStringAsync(null, "textLength", null, FormatNumber(runLength));

                var classes = BuildCssClasses(segment);
                if (!string.IsNullOrEmpty(classes))
                {
                    await xml.WriteAttributeStringAsync(null, "class", null, classes);
                }

                // Reverse video paints the text in the (resolved) background color; BuildCssClasses
                // drops the foreground color class for reversed runs so this inline fill is not
                // overridden by a CSS rule.
                if (segment.IsReverse)
                {
                    await xml.WriteAttributeStringAsync(null, "fill", null, ResolveReverseTextFill(segment));
                }
                // Inline color for RGB colors
                else if (segment.ForegroundColor != null && segment.ForegroundColor.StartsWith('#'))
                {
                    await xml.WriteAttributeStringAsync(null, "fill", null, OptimizeHexColor(segment.ForegroundColor));
                }
                else if (segment.ForegroundColor != null && int.TryParse(segment.ForegroundColor, out var idx) && idx >= 16)
                {
                    var rgb = PaletteIndexToRgb(idx);
                    if (rgb != null)
                        await xml.WriteAttributeStringAsync(null, "fill", null, rgb);
                }

                var decoration = BuildTextDecoration(segment);
                if (decoration != null)
                {
                    await xml.WriteAttributeStringAsync(null, "text-decoration", null, decoration);
                }

                await xml.WriteStringAsync(segment.Text);
                await xml.WriteEndElementAsync(); // text
            }

            cumulativeCellWidth += segment.CellWidth;
        }
    }

    /// <summary>
    /// Gets the foreground color as a hex string for custom glyph rendering.
    /// </summary>
    private string GetForegroundColorHex(StyleRun run)
    {
        if (run.ForegroundColor == null)
        {
            return FgFill(_options.Theme.Foreground);
        }

        if (run.ForegroundColor.StartsWith('#'))
        {
            return OptimizeHexColor(run.ForegroundColor);
        }

        if (int.TryParse(run.ForegroundColor, out var idx))
        {
            if (idx < 16)
            {
                // Basic ANSI colors - use theme colors
                var themeColor = idx switch
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
                    _ => _options.Theme.Foreground
                };
                return ThemeColorFill(GetAnsiColorClass(idx), themeColor);
            }

            // Extended palette
            var rgb = PaletteIndexToRgb(idx);
            if (rgb != null) return rgb;
        }

        return FgFill(_options.Theme.Foreground);
    }

    /// <summary>
    /// The fill for a run's background rect, honoring reverse video (SGR 7): a reversed run paints
    /// its (resolved) foreground color as the background — falling back to the theme foreground when
    /// the run has no explicit foreground. Returns null when there is no background to draw.
    /// </summary>
    private string? ResolveBackgroundFill(StyleRun run)
    {
        if (run.IsReverse)
        {
            return run.ForegroundColor != null
                ? ConvertColorToHex(run.ForegroundColor)
                : FgFill(_options.Theme.Foreground);
        }

        return run.BackgroundColor != null ? ConvertColorToHex(run.BackgroundColor) : null;
    }

    /// <summary>
    /// The text fill for a reversed run (SGR 7): the run's (resolved) background color, falling back
    /// to the theme background when the run has no explicit background.
    /// </summary>
    private string ResolveReverseTextFill(StyleRun run) =>
        run.BackgroundColor != null ? ConvertColorToHex(run.BackgroundColor) : BgFill(_options.Theme.Background);

    /// <summary>
    /// Builds render segments from a row of cells, splitting at custom glyph boundaries.
    /// This ensures custom glyphs are rendered separately from regular text.
    /// </summary>
    private static List<StyleRun> BuildRenderSegments(TerminalCell[] cells)
    {
        var segments = new List<StyleRun>();
        var currentRun = new StyleRun();
        bool? currentIsGlyph = null;

        foreach (var cell in cells)
        {
            // Skip continuation cells (width 0) - these are placeholders after wide characters
            if (cell.Width == 0)
                continue;

            var isGlyph = CustomGlyphRenderer.IsCustomGlyph(cell.Character);

            // Start new segment if:
            // 1. Glyph type changed (text vs custom glyph)
            // 2. Style changed (foreground, background, bold, etc.)
            var needNewSegment = currentRun.Text.Length > 0 && (
                isGlyph != currentIsGlyph ||
                cell.ForegroundColor != currentRun.ForegroundColor ||
                cell.BackgroundColor != currentRun.BackgroundColor ||
                cell.IsBold != currentRun.IsBold ||
                cell.IsItalic != currentRun.IsItalic ||
                cell.IsUnderline != currentRun.IsUnderline ||
                cell.IsReverse != currentRun.IsReverse ||
                cell.IsDim != currentRun.IsDim ||
                cell.IsStrikethrough != currentRun.IsStrikethrough ||
                cell.IsOverline != currentRun.IsOverline ||
                cell.IsConceal != currentRun.IsConceal
            );

            if (needNewSegment)
            {
                segments.Add(currentRun);
                currentRun = new StyleRun();
            }

            if (currentRun.Text.Length == 0)
            {
                currentRun.ForegroundColor = cell.ForegroundColor;
                currentRun.BackgroundColor = cell.BackgroundColor;
                currentRun.IsBold = cell.IsBold;
                currentRun.IsItalic = cell.IsItalic;
                currentRun.IsUnderline = cell.IsUnderline;
                currentRun.IsReverse = cell.IsReverse;
                currentRun.IsDim = cell.IsDim;
                currentRun.IsStrikethrough = cell.IsStrikethrough;
                currentRun.IsOverline = cell.IsOverline;
                currentRun.IsConceal = cell.IsConceal;
                currentRun.IsCustomGlyph = isGlyph;
                currentIsGlyph = isGlyph;
            }

            currentRun.Text += cell.Character;
            currentRun.CellWidth += cell.Width;
        }

        if (currentRun.Text.Length > 0)
        {
            segments.Add(currentRun);
        }

        // Trim trailing spaces from text segments (not from custom glyph segments)
        // But don't trim if the segment has a background color (Canvas uses colored spaces),
        // nor if it is reverse-video (a reversed space paints a foreground-colored block).
        if (segments.Count > 0)
        {
            var lastRun = segments[^1];
            if (!lastRun.IsCustomGlyph && lastRun.BackgroundColor == null && !lastRun.IsReverse)
            {
                var originalLength = lastRun.Text.Length;
                lastRun.Text = lastRun.Text.TrimEnd();
                lastRun.CellWidth -= (originalLength - lastRun.Text.Length);
            }
        }

        // Remove empty segments (but keep segments with background colors or reverse video -
        // both render visible colored spaces)
        while (segments.Count > 0 &&
               string.IsNullOrWhiteSpace(segments[^1].Text) &&
               segments[^1].BackgroundColor == null &&
               !segments[^1].IsReverse)
        {
            segments.RemoveAt(segments.Count - 1);
        }

        return segments;
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
            await WriteRepeatCountAsync(xml, freezeEligible: true);
            await xml.WriteEndElementAsync();

            // Y position animation
            await xml.WriteStartElementAsync(null, "animate", null);
            await xml.WriteAttributeStringAsync(null, "attributeName", null, "y");
            await xml.WriteAttributeStringAsync(null, "values", null, yValues);
            await xml.WriteAttributeStringAsync(null, "keyTimes", null, keyTimes);
            await xml.WriteAttributeStringAsync(null, "dur", null, $"{FormatTime(totalDuration)}s");
            await xml.WriteAttributeStringAsync(null, "calcMode", null, "discrete");
            await WriteRepeatCountAsync(xml, freezeEligible: true);
            await xml.WriteEndElementAsync();
        }

        // Cursor blink animation (only if enabled)
        if (_options.CursorBlink)
        {
            await xml.WriteStartElementAsync(null, "animate", null);
            await xml.WriteAttributeStringAsync(null, "attributeName", null, "opacity");
            await xml.WriteAttributeStringAsync(null, "values", null, "1;0;1");
            await xml.WriteAttributeStringAsync(null, "keyTimes", null, "0;0.5;1");
            await xml.WriteAttributeStringAsync(null, "dur", null, "1s");
            await xml.WriteAttributeStringAsync(null, "repeatCount", null, "indefinite");
            await xml.WriteEndElementAsync();
        }

        await xml.WriteEndElementAsync(); // rect
    }

    /// <summary>
    /// Writes CSS styles (minimal - SMIL handles animations).
    /// </summary>
    private async Task WriteStylesAsync(XmlWriter xml)
    {
        await xml.WriteStartElementAsync(null, "style", null);

        var css = new StringBuilder();

        // CSS custom-property palette (only when CssVariables is enabled) so the embedding
        // page can recolor / light-dark-swap the SVG with no regeneration.
        if (_options.CssVariables)
        {
            AppendCssVariableRoot(css);
        }

        // Base text styles (baseline is pre-calculated into y position for cross-browser compatibility)
        css.Append($"text{{white-space:pre;font-family:{_options.FontFamily};font-size:{_options.FontSize}px;letter-spacing:0;word-spacing:0;text-rendering:geometricPrecision;font-variant-ligatures:none}}");
        css.Append($".fg{{fill:{FgFill(_options.Theme.Foreground)}}}");

        // ANSI color classes
        AppendAnsiColorStyles(css);

        // Style flags
        // underline/strikethrough/overline are emitted as a combined text-decoration presentation
        // attribute per run (see BuildTextDecoration), so no decoration CSS class is needed here.
        css.Append(".bold{font-weight:bold}.italic{font-style:italic}.dim{fill-opacity:0.55}");

        // Box-drawing line styles. The stroke width, square linecap and fill:none are identical across
        // every light/heavy box glyph, so CustomGlyphRenderer emits class="bl"/"bh" and they live here
        // once instead of inline on each path — the single biggest SVG-size lever on glyph-heavy output
        // (e.g. a truecolor gradient progress bar is tens of thousands of these paths). Widths must match
        // the per-row formula in RenderRowContentAsync.
        var lightStroke = Math.Max(1, _charHeight * 0.08);
        var heavyStroke = Math.Max(2, _charHeight * 0.16);
        css.Append($".bl{{fill:none;stroke-linecap:square;stroke-width:{FormatStroke(lightStroke)}}}");
        css.Append($".bh{{fill:none;stroke-linecap:square;stroke-width:{FormatStroke(heavyStroke)}}}");

        // Cursor (follows the foreground/--vcr-fg, matching legacy behavior)
        if (!_options.DisableCursor)
        {
            css.Append($".cursor-block{{fill:{FgFill(_options.Theme.Foreground)}}}");
        }

        await xml.WriteStringAsync(css.ToString());
        await xml.WriteEndElementAsync();
    }

    /// <summary>
    /// Writes pattern definitions for shade block elements (░▒▓).
    /// </summary>
    private async Task WriteShadePatternDefsAsync(XmlWriter xml)
    {
        var fg = FgFill(_options.Theme.Foreground);

        // Light shade ░ - sparse dots (25% coverage)
        await xml.WriteStartElementAsync(null, "pattern", null);
        await xml.WriteAttributeStringAsync(null, "id", null, "shade-light");
        await xml.WriteAttributeStringAsync(null, "width", null, "4");
        await xml.WriteAttributeStringAsync(null, "height", null, "4");
        await xml.WriteAttributeStringAsync(null, "patternUnits", null, "userSpaceOnUse");
        await xml.WriteStartElementAsync(null, "rect", null);
        await xml.WriteAttributeStringAsync(null, "width", null, "1");
        await xml.WriteAttributeStringAsync(null, "height", null, "1");
        await xml.WriteAttributeStringAsync(null, "fill", null, fg);
        await xml.WriteEndElementAsync(); // rect
        await xml.WriteEndElementAsync(); // pattern

        // Medium shade ▒ - checkerboard (50% coverage)
        await xml.WriteStartElementAsync(null, "pattern", null);
        await xml.WriteAttributeStringAsync(null, "id", null, "shade-medium");
        await xml.WriteAttributeStringAsync(null, "width", null, "2");
        await xml.WriteAttributeStringAsync(null, "height", null, "2");
        await xml.WriteAttributeStringAsync(null, "patternUnits", null, "userSpaceOnUse");
        await xml.WriteStartElementAsync(null, "rect", null);
        await xml.WriteAttributeStringAsync(null, "width", null, "1");
        await xml.WriteAttributeStringAsync(null, "height", null, "1");
        await xml.WriteAttributeStringAsync(null, "fill", null, fg);
        await xml.WriteEndElementAsync(); // rect
        await xml.WriteStartElementAsync(null, "rect", null);
        await xml.WriteAttributeStringAsync(null, "x", null, "1");
        await xml.WriteAttributeStringAsync(null, "y", null, "1");
        await xml.WriteAttributeStringAsync(null, "width", null, "1");
        await xml.WriteAttributeStringAsync(null, "height", null, "1");
        await xml.WriteAttributeStringAsync(null, "fill", null, fg);
        await xml.WriteEndElementAsync(); // rect
        await xml.WriteEndElementAsync(); // pattern

        // Dark shade ▓ - dense pattern (75% coverage)
        await xml.WriteStartElementAsync(null, "pattern", null);
        await xml.WriteAttributeStringAsync(null, "id", null, "shade-dark");
        await xml.WriteAttributeStringAsync(null, "width", null, "2");
        await xml.WriteAttributeStringAsync(null, "height", null, "2");
        await xml.WriteAttributeStringAsync(null, "patternUnits", null, "userSpaceOnUse");
        await xml.WriteStartElementAsync(null, "rect", null);
        await xml.WriteAttributeStringAsync(null, "width", null, "2");
        await xml.WriteAttributeStringAsync(null, "height", null, "2");
        await xml.WriteAttributeStringAsync(null, "fill", null, fg);
        await xml.WriteEndElementAsync(); // rect
        await xml.WriteStartElementAsync(null, "rect", null);
        await xml.WriteAttributeStringAsync(null, "x", null, "1");
        await xml.WriteAttributeStringAsync(null, "y", null, "1");
        await xml.WriteAttributeStringAsync(null, "width", null, "1");
        await xml.WriteAttributeStringAsync(null, "height", null, "1");
        await xml.WriteAttributeStringAsync(null, "fill", null, BgFill(_options.Theme.Background));
        await xml.WriteEndElementAsync(); // rect
        await xml.WriteEndElementAsync(); // pattern
    }

    private void AppendAnsiColorStyles(StringBuilder css)
    {
        foreach (var (letter, color) in AnsiPalette())
        {
            css.Append($".{letter}{{fill:{ThemeColorFill(letter, color)}}}");
        }
    }

    /// <summary>
    /// The 16 ANSI colors paired with their single-letter class/variable suffix
    /// (k/r/g/y/b/m/c/w normal, K/R/G/Y/B/M/C/W bright). Single source of truth for
    /// both the CSS class definitions and the :root custom-property block.
    /// </summary>
    private (string Letter, string Color)[] AnsiPalette() =>
    [
        ("k", _options.Theme.Black), ("r", _options.Theme.Red), ("g", _options.Theme.Green), ("y", _options.Theme.Yellow),
        ("b", _options.Theme.Blue), ("m", _options.Theme.Magenta), ("c", _options.Theme.Cyan), ("w", _options.Theme.White),
        ("K", _options.Theme.BrightBlack), ("R", _options.Theme.BrightRed), ("G", _options.Theme.BrightGreen), ("Y", _options.Theme.BrightYellow),
        ("B", _options.Theme.BrightBlue), ("M", _options.Theme.BrightMagenta), ("C", _options.Theme.BrightCyan), ("W", _options.Theme.BrightWhite),
    ];

    /// <summary>
    /// Writes the :root CSS custom-property block (--vcr-bg/--vcr-fg + 16 ANSI vars).
    /// </summary>
    private void AppendCssVariableRoot(StringBuilder css)
    {
        css.Append(":root{");
        css.Append($"--vcr-bg:{OptimizeHexColor(_options.Theme.Background)};");
        css.Append($"--vcr-fg:{OptimizeHexColor(_options.Theme.Foreground)};");
        foreach (var (letter, color) in AnsiPalette())
        {
            css.Append($"--vcr-{letter}:{OptimizeHexColor(color)};");
        }
        css.Append('}');
    }

    /// <summary>Resolves an ANSI palette color to either a literal hex or a var(--vcr-letter,#hex).</summary>
    private string ThemeColorFill(string letter, string fallback) =>
        _options.CssVariables ? $"var(--vcr-{letter},{OptimizeHexColor(fallback)})" : OptimizeHexColor(fallback);

    /// <summary>Resolves the foreground color to either a literal hex or var(--vcr-fg,#hex).</summary>
    private string FgFill(string fallback) =>
        _options.CssVariables ? $"var(--vcr-fg,{OptimizeHexColor(fallback)})" : OptimizeHexColor(fallback);

    /// <summary>Resolves the background color to either a literal hex or var(--vcr-bg,#hex).</summary>
    private string BgFill(string fallback) =>
        _options.CssVariables ? $"var(--vcr-bg,{OptimizeHexColor(fallback)})" : OptimizeHexColor(fallback);

    private void CalculateDimensions()
    {
        // With a measured cell size (browser path) use it as-is. Without one (native/browserless path)
        // estimate from the font size — and ROUND to whole pixels: a fractional cell width makes every
        // background rect land on a sub-pixel boundary, so adjacent same-color cells (e.g. bar-chart
        // fills) anti-alias independently and show hairline seams. Integer cells tile cleanly and match
        // the browser's measured advance (~0.6 × font-size for typical monospace fonts).
        _charWidth = _options.ActualCellWidth ?? Math.Round(_options.FontSize * 0.6);
        _charHeight = _options.ActualCellHeight ?? Math.Round(_options.FontSize * 1.2);

        // Canvas defaults to the configured viewport. When FitToContent supplies a
        // content extent, the canvas shrinks to fit measured content plus padding.
        if (_cropCols.HasValue && _cropRows.HasValue)
        {
            _canvasWidth = (int)Math.Ceiling(_cropCols.Value * _charWidth) + 2 * _options.Padding;
            _canvasHeight = (int)Math.Ceiling(_cropRows.Value * _charHeight) + 2 * _options.Padding;
        }
        else
        {
            _canvasWidth = _options.Width;
            _canvasHeight = _options.Height;

            // Guard against the configured viewport being smaller than the content we actually
            // captured. The terminal's real column/row count can exceed the requested Cols/Rows
            // (e.g. ttyd re-fits the terminal to the window after we resize it, or the measured
            // cell size differs slightly from xterm's), so cells laid out at col*_charWidth can
            // extend past _options.Width. Without this, those trailing columns - like a table's
            // right border - fall outside the viewBox/clip and are silently cut off. Grow (never
            // shrink) the canvas so it contains every rendered cell.
            if (_measuredExtentCols.HasValue)
                _canvasWidth = Math.Max(_canvasWidth,
                    (int)Math.Ceiling(_measuredExtentCols.Value * _charWidth) + 2 * _options.Padding);
            if (_measuredExtentRows.HasValue)
                _canvasHeight = Math.Max(_canvasHeight,
                    (int)Math.Ceiling(_measuredExtentRows.Value * _charHeight) + 2 * _options.Padding);
        }

        _frameWidth = _canvasWidth - 2 * _options.Padding;
        _frameHeight = _canvasHeight - 2 * _options.Padding;
    }

    /// <summary>
    /// Overrides the rendered canvas to fit the given content extent (in character cells),
    /// cropping trailing blank rows and right-side blank columns. Used by FitToContent.
    /// </summary>
    public void SetContentExtent(int cols, int rows)
    {
        _cropCols = Math.Max(cols, 1);
        _cropRows = Math.Max(rows, 1);
        CalculateDimensions();
    }

    /// <summary>
    /// Grows the canvas (never shrinks it) so it contains the given measured content extent,
    /// preventing trailing columns/rows from being clipped when the configured viewport is
    /// smaller than the content the terminal actually produced (see <see cref="CalculateDimensions"/>).
    /// No-op in FitToContent mode, where the explicit crop extent already drives the canvas size.
    /// </summary>
    private void EnsureCanvasFitsContent(ContentExtent extent)
    {
        if (FitMode) return;
        _measuredExtentCols = extent.Cols;
        _measuredExtentRows = extent.Rows;
        CalculateDimensions();
    }

    /// <summary>True when fit-to-content cropping is active (an extent has been supplied).</summary>
    private bool FitMode => _cropRows.HasValue;

    /// <summary>
    /// Writes the repeatCount for an animation driven by Set Loop / Set LoopCount.
    /// When the loop is finite (not "indefinite") and the animation can hold its final
    /// value, also writes fill="freeze" so the reveal plays once (or N times) and holds
    /// the final frame instead of snapping back to the start.
    /// </summary>
    private async Task WriteRepeatCountAsync(XmlWriter xml, bool freezeEligible)
    {
        var repeatCount = _options.ResolveSvgRepeatCount();
        await xml.WriteAttributeStringAsync(null, "repeatCount", null, repeatCount);
        if (freezeEligible && repeatCount != "indefinite")
        {
            await xml.WriteAttributeStringAsync(null, "fill", null, "freeze");
        }
    }

    /// <summary>
    /// Writes the root &lt;svg&gt; attributes: always viewBox; optionally explicit intrinsic
    /// width/height (px) and machine-readable data-* metadata. Single write site shared by
    /// the static and animated renderers so size/metadata stay consistent. contentCols/Rows
    /// are the authoritative grid size; when fit-to-content is active the cropped extent is
    /// reported instead.
    /// </summary>
    private async Task WriteRootSvgAttributesAsync(XmlWriter xml, int contentCols, int contentRows)
    {
        await xml.WriteAttributeStringAsync(null, "viewBox", null, $"0 0 {_canvasWidth} {_canvasHeight}");

        if (_options.SvgIntrinsicSize)
        {
            await xml.WriteAttributeStringAsync(null, "width", null, _canvasWidth.ToString(CultureInfo.InvariantCulture));
            await xml.WriteAttributeStringAsync(null, "height", null, _canvasHeight.ToString(CultureInfo.InvariantCulture));
        }

        if (_options.SvgMetadata)
        {
            var cols = _cropCols ?? contentCols;
            var rows = _cropRows ?? contentRows;
            await xml.WriteAttributeStringAsync(null, "data-cols", null, cols.ToString(CultureInfo.InvariantCulture));
            await xml.WriteAttributeStringAsync(null, "data-rows", null, rows.ToString(CultureInfo.InvariantCulture));
            await xml.WriteAttributeStringAsync(null, "data-font-size", null, _options.FontSize.ToString(CultureInfo.InvariantCulture));
            await xml.WriteAttributeStringAsync(null, "data-cell-width", null, FormatNumber(_charWidth));
            await xml.WriteAttributeStringAsync(null, "data-cell-height", null, FormatNumber(_charHeight));
            await xml.WriteAttributeStringAsync(null, "data-padding", null, _options.Padding.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// A fast 64-bit content fingerprint for a row, used only as a dedup/equality key when collapsing
    /// identical row states across frames. Covers exactly the fields the SVG renderer draws per row.
    /// FNV-1a (not a crypto hash) — this runs per row per frame, so MD5's allocation + digest cost was
    /// pure overhead; 64-bit collision odds are negligible for the handful of distinct states per row.
    /// </summary>
    private static ulong ComputeRowHash(TerminalCell[] cells)
    {
        const ulong fnvOffset = 14695981039346656037UL;
        const ulong fnvPrime = 1099511628211UL;

        var hash = fnvOffset;

        static ulong MixString(ulong h, string? s)
        {
            if (s != null)
                foreach (var ch in s)
                    h = (h ^ ch) * fnvPrime;
            // Field separator so adjacent fields can't merge into a colliding key (e.g. fg "a"+bg "b"
            // vs fg "ab"+bg "").
            return (h ^ 0xFFFF) * fnvPrime;
        }

        foreach (var cell in cells)
        {
            hash = MixString(hash, cell.Character);
            hash = MixString(hash, cell.ForegroundColor);
            hash = MixString(hash, cell.BackgroundColor);

            var flags = (cell.IsBold ? 1 : 0)
                      | (cell.IsItalic ? 1 << 1 : 0)
                      | (cell.IsUnderline ? 1 << 2 : 0)
                      | (cell.IsReverse ? 1 << 3 : 0)
                      | (cell.IsDim ? 1 << 4 : 0)
                      | (cell.IsStrikethrough ? 1 << 5 : 0)
                      | (cell.IsOverline ? 1 << 6 : 0)
                      | (cell.IsConceal ? 1 << 7 : 0);
            hash = (hash ^ (uint)flags) * fnvPrime;
        }

        return hash;
    }

    private static string BuildCssClasses(StyleRun run)
    {
        var classes = new List<string>();

        // Reverse video resolves its foreground to the (swapped) background color via an inline
        // fill in the text pass, so it must NOT carry a foreground color class — a CSS class fill
        // would win over the inline presentation attribute and undo the swap.
        if (!run.IsReverse)
        {
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
        }

        if (run.IsBold) classes.Add("bold");
        if (run.IsItalic) classes.Add("italic");
        // Dim is an opacity reduction independent of how the fill is set, so it composes with a
        // foreground class, an inline RGB fill, and reverse video (which dims the swapped text).
        if (run.IsDim) classes.Add("dim");
        // Underline / strikethrough / overline are all text-decoration lines and must be able to
        // combine, so they are emitted together via BuildTextDecoration (a single presentation
        // attribute) rather than as conflicting CSS classes.

        return string.Join(" ", classes);
    }

    /// <summary>
    /// The combined <c>text-decoration</c> value for a run (underline / line-through / overline),
    /// or null when none apply. One declaration so the lines compose; the decoration follows the
    /// run's text fill (currentColor) like a real terminal.
    /// </summary>
    private static string? BuildTextDecoration(StyleRun run)
    {
        var lines = new List<string>();
        if (run.IsUnderline) lines.Add("underline");
        if (run.IsOverline) lines.Add("overline");
        if (run.IsStrikethrough) lines.Add("line-through");
        return lines.Count > 0 ? string.Join(" ", lines) : null;
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

    /// <summary>
    /// Formats a stroke width for the box-drawing CSS classes. Mirrors <c>CustomGlyphRenderer.F</c>
    /// (whole numbers bare, otherwise two decimals) so the class width equals what the glyph paths
    /// were previously emitting inline.
    /// </summary>
    private static string FormatStroke(double value) =>
        Math.Abs(value % 1) < 0.001
            ? ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture)
            : value.ToString("F2", CultureInfo.InvariantCulture);

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
                return ThemeColorFill(GetAnsiColorClass(paletteIndex), themeColor);
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
        public required ulong Hash { get; init; }
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
        public bool IsReverse { get; set; }
        public bool IsDim { get; set; }
        public bool IsStrikethrough { get; set; }
        public bool IsOverline { get; set; }
        public bool IsConceal { get; set; }
        /// <summary>
        /// Total cell width of this run (accounts for wide characters taking 2 cells).
        /// </summary>
        public int CellWidth { get; set; }
        /// <summary>
        /// Whether this run contains custom glyph characters (box drawing, block elements, powerline).
        /// </summary>
        public bool IsCustomGlyph { get; set; }
    }
}
