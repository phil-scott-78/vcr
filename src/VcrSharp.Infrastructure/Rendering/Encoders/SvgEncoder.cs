using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using VcrSharp.Core.Recording;
using VcrSharp.Core.Rendering;
using VcrSharp.Core.Session;
using VcrSharp.Infrastructure.Recording;

namespace VcrSharp.Infrastructure.Rendering.Encoders;

/// <summary>
/// Encoder that renders SVG output with text-based animation.
/// Follows AgentStation/vHS approach: text rendered as SVG elements, frame deduplication, CSS animations.
/// See - https://github.com/agentstation/vhs/blob/main/svg.go
///
/// This works surprisingly well, but still rough around the edges especially with the cursor.
/// </summary>
public class SvgEncoder(SessionOptions options, FrameStorage storage) : EncoderBase(options, storage)
{
    // State management for deduplication
    private readonly Dictionary<string, int> _stateHashes = new();
    private readonly List<TerminalState> _uniqueStates = [];
    private readonly List<KeyframeStop> _timeline = [];

    // Dimension calculations
    private double _charWidth;
    private double _charHeight;
    private int _frameWidth;
    private int _frameHeight;

    public override bool SupportsPath(string outputPath)
    {
        return Path.GetExtension(outputPath).Equals(".svg", StringComparison.OrdinalIgnoreCase);
    }

    public override async Task<string> RenderAsync(string outputPath, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        ValidateFramesExist();

        progress?.Report("Loading terminal content snapshots...");

        // Get terminal content snapshots
        var snapshots = GetTerminalSnapshots();
        var frameMetadata = GetFrameMetadata();

        if (snapshots.Count == 0)
        {
            throw new InvalidOperationException("No terminal content snapshots found. SVG encoder requires terminal content capture during recording.");
        }

        progress?.Report($"Processing {snapshots.Count} frames...");

        // Calculate dimensions
        CalculateDimensions();

        // Process frames and build timeline
        ProcessFrames(snapshots, frameMetadata);

        progress?.Report($"Deduplication: {snapshots.Count} frames -> {_uniqueStates.Count} unique states");

        // Generate SVG
        progress?.Report("Generating SVG...");
        await GenerateSvgAsync(outputPath, cancellationToken);

        progress?.Report($"SVG exported to {outputPath}");

        return outputPath;
    }

    /// <summary>
    /// Calculates character and frame dimensions based on session options.
    /// </summary>
    private void CalculateDimensions()
    {
        // Use actual measured cell dimensions from xterm.js if available,
        // otherwise fall back to estimation based on font size
        // Note: 0.55 multiplier matches VHS implementation to prevent cursor drift
        _charWidth = Options.ActualCellWidth ?? Options.FontSize * 0.55;
        _charHeight = Options.ActualCellHeight ?? Options.FontSize * 1.2;

        // Frame dimensions (terminal area without padding)
        _frameWidth = Options.Width - 2 * Options.Padding;
        _frameHeight = Options.Height - 2 * Options.Padding;
    }

    /// <summary>
    /// Processes frames, performs deduplication, and builds animation timeline.
    /// </summary>
    private void ProcessFrames(IReadOnlyList<TerminalContentSnapshot> snapshots, IReadOnlyList<FrameMetadata> metadata)
    {
        TerminalContent? previousContent = null;
        int? previousCursorX = null;
        int? previousCursorY = null;
        TimeSpan? cursorIdleStartTime = null;

        foreach (var snapshot in snapshots)
        {
            var content = snapshot.Content;

            // Skip null, empty, or invalid content
            if (content == null || content.Rows == 0 || content.Cols == 0 || content.Cells.Length == 0)
                continue;

            // Cursor idle detection
            var isCursorIdle = false;
            if (previousContent != null)
            {
                // Check if cursor moved or text changed at cursor position
                var cursorMoved = content.CursorX != previousCursorX || content.CursorY != previousCursorY;
                var textChanged = HasTextChangedAtCursor(previousContent, content);

                if (cursorMoved || textChanged)
                {
                    // Cursor activity - reset idle timer
                    cursorIdleStartTime = null;
                }
                else
                {
                    // Cursor stationary
                    if (cursorIdleStartTime == null)
                    {
                        cursorIdleStartTime = snapshot.Timestamp;
                    }
                    else
                    {
                        // Check if idle for > 0.5 seconds
                        var idleDuration = snapshot.Timestamp - cursorIdleStartTime.Value;
                        if (idleDuration.TotalSeconds > 0.5)
                        {
                            isCursorIdle = true;
                        }
                    }
                }
            }

            // Compute hash for deduplication
            var hash = ComputeFrameHash(content, isCursorIdle);

            // Check if this state already exists
            if (!_stateHashes.TryGetValue(hash, out var stateIndex))
            {
                // New unique state
                stateIndex = _uniqueStates.Count;
                _stateHashes[hash] = stateIndex;
                _uniqueStates.Add(new TerminalState
                {
                    Content = content,
                    IsCursorIdle = isCursorIdle
                });
            }

            // Add to timeline
            var snapshot1 = snapshot;
            var frameMeta = metadata.FirstOrDefault(m => m.FrameNumber == snapshot1.FrameNumber);
            if (frameMeta is { IsVisible: true })
            {
                _timeline.Add(new KeyframeStop
                {
                    Timestamp = snapshot.Timestamp,
                    StateIndex = stateIndex
                });
            }

            previousContent = content;
            previousCursorX = content.CursorX;
            previousCursorY = content.CursorY;
        }
    }

    /// <summary>
    /// Checks if text changed at the cursor position.
    /// </summary>
    private static bool HasTextChangedAtCursor(TerminalContent prev, TerminalContent curr)
    {
        if (prev.CursorY >= prev.Rows || curr.CursorY >= curr.Rows)
            return false;

        if (prev.CursorX >= prev.Cols || curr.CursorX >= curr.Cols)
            return false;

        var prevCell = prev.Cells[prev.CursorY][prev.CursorX];
        var currCell = curr.Cells[curr.CursorY][curr.CursorX];

        return prevCell.Character != currCell.Character;
    }

    /// <summary>
    /// Generates the complete SVG file with animation.
    /// </summary>
    private async Task GenerateSvgAsync(string outputPath, CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);
        await using var xml = XmlWriter.Create(writer, new XmlWriterSettings
        {
            Async = true,
            Indent = false,  // Minimize file size
            OmitXmlDeclaration = false,
            Encoding = Encoding.UTF8
        });

        // Calculate animation duration
        var totalDuration = _timeline.Count > 0 ? _timeline[^1].Timestamp.TotalSeconds / Options.PlaybackSpeed : 1.0;

        // Outer SVG with viewBox for responsive scaling
        await xml.WriteStartElementAsync(null, "svg", "http://www.w3.org/2000/svg");
        await xml.WriteAttributeStringAsync(null, "viewBox", null, $"0 0 {Options.Width} {Options.Height}");

        // Styles and animations
        await WriteStylesAndAnimationsAsync(xml, totalDuration);

        // Background (skip if transparent background is enabled)
        if (!Options.TransparentBackground)
        {
            await xml.WriteStartElementAsync(null, "rect", null);
            await xml.WriteAttributeStringAsync(null, "width", null, "100%");
            await xml.WriteAttributeStringAsync(null, "height", null, "100%");
            await xml.WriteAttributeStringAsync(null, "fill", null, OptimizeHexColor(Options.Theme.Background));
            await xml.WriteEndElementAsync();
        }

        // Inner SVG with viewBox (shows one frame at a time)
        await xml.WriteStartElementAsync(null, "svg", null);
        await xml.WriteAttributeStringAsync(null, "x", null, FormatNumber(Options.Padding));
        await xml.WriteAttributeStringAsync(null, "y", null, FormatNumber(Options.Padding));
        await xml.WriteAttributeStringAsync(null, "width", null, _frameWidth.ToString(CultureInfo.InvariantCulture));
        await xml.WriteAttributeStringAsync(null, "height", null, _frameHeight.ToString(CultureInfo.InvariantCulture));
        await xml.WriteAttributeStringAsync(null, "viewBox", null, $"0 0 {_frameWidth} {_frameHeight}");

        // Animation container with translateX
        await xml.WriteStartElementAsync(null, "g", null);
        await xml.WriteAttributeStringAsync(null, "class", null, "animation-container");

        // Render each unique state
        for (var i = 0; i < _uniqueStates.Count; i++)
        {
            var state = _uniqueStates[i];
            var xOffset = i * _frameWidth;

            await RenderTerminalStateAsync(xml, state, xOffset);
        }

        await xml.WriteEndElementAsync(); // g animation-container
        await xml.WriteEndElementAsync(); // svg inner
        await xml.WriteEndElementAsync(); // svg outer

        await xml.FlushAsync();
    }

    /// <summary>
    /// Writes CSS styles and keyframe animations.
    /// </summary>
    private async Task WriteStylesAndAnimationsAsync(XmlWriter xml, double totalDuration)
    {
        await xml.WriteStartElementAsync(null, "style", null);

        var css = new StringBuilder();

        // Base styles (minified - no newlines between rules)
        css.Append($"text{{white-space:pre;font-family:{Options.FontFamily};font-size:{Options.FontSize}px}}");
        css.Append($".fg{{fill:{OptimizeHexColor(Options.Theme.Foreground)}}}");

        // ANSI color classes
        var colors = new Dictionary<string, string>
        {
            ["k"] = Options.Theme.Black,
            ["r"] = Options.Theme.Red,
            ["g"] = Options.Theme.Green,
            ["y"] = Options.Theme.Yellow,
            ["b"] = Options.Theme.Blue,
            ["m"] = Options.Theme.Magenta,
            ["c"] = Options.Theme.Cyan,
            ["w"] = Options.Theme.White,
            ["K"] = Options.Theme.BrightBlack,
            ["R"] = Options.Theme.BrightRed,
            ["G"] = Options.Theme.BrightGreen,
            ["Y"] = Options.Theme.BrightYellow,
            ["B"] = Options.Theme.BrightBlue,
            ["M"] = Options.Theme.BrightMagenta,
            ["C"] = Options.Theme.BrightCyan,
            ["W"] = Options.Theme.BrightWhite
        };

        foreach (var (name, color) in colors)
        {
            css.Append($".{name}{{fill:{OptimizeHexColor(color)}}}");
        }

        // Style flags (minified)
        css.Append(".bold{font-weight:bold}.italic{font-style:italic}.underline{text-decoration:underline}");

        // Cursor styles (minified)
        css.Append("@keyframes blink{0%,49%{opacity:1}50%,100%{opacity:0}}");
        css.Append(".cursor-idle{animation:blink 1s steps(1) infinite}");
        css.Append($".cursor-block{{fill:{OptimizeHexColor(Options.Theme.Foreground)}}}");

        // Slide animation keyframes
        if (_timeline.Count > 0)
        {
            css.AppendLine("@keyframes slide{");

            int? lastStateIndex = null;
            foreach (var stop in _timeline)
            {
                // Skip consecutive duplicate states to reduce file size
                if (lastStateIndex.HasValue && stop.StateIndex == lastStateIndex.Value)
                    continue;

                var percentage = stop.Timestamp.TotalSeconds / Options.PlaybackSpeed / totalDuration * 100.0;
                var offset = -1 * stop.StateIndex * _frameWidth;

                // Use adaptive precision
                var precision = _uniqueStates.Count < 100 ? 1 : _uniqueStates.Count < 1000 ? 2 : 4;
                var percentStr = percentage.ToString("F" + precision, CultureInfo.InvariantCulture);

                css.AppendLine($"{percentStr}%{{transform:translateX({offset}px);}}");
                lastStateIndex = stop.StateIndex;
            }

            css.AppendLine("}");

            // Apply animation to container (minified)
            var loopOffset = Options.LoopOffset;
            css.Append($".animation-container{{animation:slide {totalDuration}s step-end {loopOffset}s infinite}}");
        }

        await xml.WriteStringAsync(css.ToString());
        await xml.WriteEndElementAsync(); // style
    }

    /// <summary>
    /// Renders a single terminal state as SVG.
    /// </summary>
    private async Task RenderTerminalStateAsync(XmlWriter xml, TerminalState state, int xOffset)
    {
        var content = state.Content;

        // State group with xml:space to avoid repeating on every text element
        await xml.WriteStartElementAsync(null, "g", null);
        await xml.WriteAttributeStringAsync(null, "transform", null, $"translate({xOffset},0)");
        await xml.WriteAttributeStringAsync("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");

        // Render each line
        for (var row = 0; row < content.Rows; row++)
        {
            await RenderLineAsync(xml, content, row, state.IsCursorIdle);
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

        // Render backgrounds first (if any)
        await RenderBackgroundsAsync(xml, runs, row);

        // Calculate total text length for this line
        var totalChars = runs.Sum(r => r.Text.Length);
        var textLength = totalChars * _charWidth;

        // Render text with exact length enforcement to prevent drift
        await xml.WriteStartElementAsync(null, "text", null);
        await xml.WriteAttributeStringAsync(null, "y", null, FormatNumber(y));
        await xml.WriteAttributeStringAsync(null, "textLength", null, FormatNumber(textLength));
        await xml.WriteAttributeStringAsync(null, "lengthAdjust", null, "spacingAndGlyphs");

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
            var classes = BuildCssClasses(run);
            if (!string.IsNullOrEmpty(classes))
            {
                await xml.WriteAttributeStringAsync(null, "class", null, classes);
            }

            // Inline color if not using class
            if (run.ForegroundColor != null && !IsAnsiColor(run.ForegroundColor))
            {
                await xml.WriteAttributeStringAsync(null, "fill", null, OptimizeHexColor(run.ForegroundColor));
            }

            await xml.WriteStringAsync(run.Text);
            await xml.WriteEndElementAsync(); // tspan
        }

        await xml.WriteEndElementAsync(); // text
    }

    /// <summary>
    /// Renders background rectangles for cells with background colors or cursor.
    /// </summary>
    private async Task RenderBackgroundsAsync(XmlWriter xml, List<StyleRun> runs, int row)
    {
        var col = 0;
        foreach (var run in runs)
        {
            // Render background color if present
            if (run.BackgroundColor != null)
            {
                var x = col * _charWidth;
                // Align background with text visual bounds (text baseline is at (row + 1) * _charHeight)
                var y = (row + 1) * _charHeight - _charHeight * 0.85;
                var width = run.Text.Length * _charWidth;

                await xml.WriteStartElementAsync(null, "rect", null);
                await xml.WriteAttributeStringAsync(null, "x", null, FormatNumber(x));
                await xml.WriteAttributeStringAsync(null, "y", null, FormatNumber(y));
                await xml.WriteAttributeStringAsync(null, "width", null, FormatNumber(width));
                await xml.WriteAttributeStringAsync(null, "height", null, FormatNumber(_charHeight));
                await xml.WriteAttributeStringAsync(null, "fill", null, OptimizeHexColor(run.BackgroundColor));
                await xml.WriteEndElementAsync();
            }

            // Render cursor background (only when cursor is active/visible)
            if (run is { IsCursor: true, IsCursorIdle: false })
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

        return runs;
    }

    /// <summary>
    /// Builds CSS class string for a style run.
    /// </summary>
    private static string BuildCssClasses(StyleRun run)
    {
        var classes = new List<string>();

        // Foreground color class
        if (run.ForegroundColor != null && IsAnsiColor(run.ForegroundColor))
        {
            classes.Add(GetAnsiColorClass(run.ForegroundColor));
        }
        else if (run.ForegroundColor == null)
        {
            classes.Add("fg");
        }

        // Style flags
        if (run.IsBold) classes.Add("bold");
        if (run.IsItalic) classes.Add("italic");
        if (run.IsUnderline) classes.Add("underline");

        // Cursor
        if (run is { IsCursor: true, IsCursorIdle: true })
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
            // For extended palette (16-255), use default
            _ => "fg"
        };
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
    /// Computes MD5 hash of terminal state for frame deduplication.
    /// </summary>
    private static string ComputeFrameHash(TerminalContent content, bool isCursorIdle)
    {
        var sb = new StringBuilder();

        for (var row = 0; row < content.Rows; row++)
        {
            for (var col = 0; col < content.Cols; col++)
            {
                var cell = content.Cells[row][col];
                sb.Append(cell.Character);
                sb.Append(cell.ForegroundColor ?? "");
                sb.Append(cell.BackgroundColor ?? "");
                sb.Append(cell.IsBold ? "b" : "");
                sb.Append(cell.IsItalic ? "i" : "");
                sb.Append(cell.IsUnderline ? "u" : "");
            }
            sb.AppendLine();
        }

        sb.Append($"{content.CursorX},{content.CursorY},{content.CursorVisible},{isCursorIdle}");

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = MD5.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Represents a unique terminal state.
    /// </summary>
    private sealed class TerminalState
    {
        public required TerminalContent Content { get; init; }
        public bool IsCursorIdle { get; init; }
    }

    /// <summary>
    /// Represents a keyframe in the animation timeline.
    /// </summary>
    private sealed class KeyframeStop
    {
        public required TimeSpan Timestamp { get; init; }
        public required int StateIndex { get; init; }
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
