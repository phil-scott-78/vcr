using System.Text;

namespace VcrSharp.Core.Rendering;

/// <summary>
/// Shared helpers for deciding whether terminal content is "blank" (no rendered output).
/// A single source of truth so the SVG row-skip, content-aware loop trimming, and
/// fit-to-content cropping all agree on what counts as empty.
/// </summary>
public static class ContentAnalysis
{
    /// <summary>
    /// A cell is blank when it has no visible glyph (whitespace/empty character)
    /// and no background color. This mirrors the long-standing row-skip test in
    /// the SVG renderer (whitespace text AND no background color).
    /// </summary>
    public static bool IsBlankCell(TerminalCell cell) =>
        string.IsNullOrWhiteSpace(cell.Character) && cell.BackgroundColor == null;

    /// <summary>
    /// A row is blank when every cell in it is blank.
    /// </summary>
    public static bool IsBlankRow(TerminalCell[] cells)
    {
        foreach (var cell in cells)
        {
            if (!IsBlankCell(cell))
                return false;
        }
        return true;
    }

    /// <summary>
    /// True when the entire visible terminal content is blank.
    /// </summary>
    public static bool IsBlankContent(TerminalContent? content)
    {
        if (content?.Cells == null) return true;
        foreach (var row in content.Cells)
        {
            if (!IsBlankRow(row))
                return false;
        }
        return true;
    }

    /// <summary>
    /// A stable signature of a terminal state (characters + colors + styles) used to detect
    /// frames that are identical to one another.
    /// </summary>
    public static string Signature(TerminalContent? content)
    {
        if (content?.Cells == null) return string.Empty;
        var sb = new StringBuilder();
        foreach (var row in content.Cells)
        {
            foreach (var cell in row)
            {
                sb.Append(cell.Character);
                sb.Append(cell.ForegroundColor);
                sb.Append('|');
                sb.Append(cell.BackgroundColor);
                sb.Append(cell.IsBold ? 'b' : '.');
                sb.Append(cell.IsItalic ? 'i' : '.');
                sb.Append(cell.IsUnderline ? 'u' : '.');
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>
    /// Computes the inclusive frame range [Start, End] to keep for a looping animation:
    /// drops leading fully-blank frames so t=0 already shows content (eliminating the
    /// empty-then-content flash each loop), and collapses a trailing run of frames identical
    /// to the final frame down to its first occurrence so the loop doesn't sit on dead air.
    /// At least one frame (and, when available, two) is always retained.
    /// </summary>
    public static (int Start, int End) TrimBlankLoopRange(IReadOnlyList<TerminalContent>? frames)
    {
        if (frames == null || frames.Count <= 1)
            return (0, Math.Max((frames?.Count ?? 1) - 1, 0));

        var start = 0;
        while (start < frames.Count - 1 && IsBlankContent(frames[start]))
            start++;

        var end = frames.Count - 1;
        var finalSignature = Signature(frames[end]);
        var firstOfFinal = end;
        while (firstOfFinal - 1 > start && Signature(frames[firstOfFinal - 1]) == finalSignature)
            firstOfFinal--;

        var last = firstOfFinal;
        if (last <= start)
            last = Math.Min(start + 1, frames.Count - 1);

        return (start, last);
    }
}

/// <summary>
/// The rendered extent of terminal content measured in character cells:
/// the smallest top-left-anchored grid that still contains every non-blank cell.
/// Used to crop SVG output to actual content (trim trailing blank rows and
/// right-side blank columns) and to report accurate data-cols/data-rows metadata.
/// </summary>
public readonly record struct ContentExtent(int Cols, int Rows)
{
    /// <summary>
    /// Measures the content extent of a single terminal state. Wide characters
    /// (Width == 2) advance the column cursor by two; continuation cells (Width == 0)
    /// advance by zero, so column math lands on real glyph boundaries.
    /// Always returns at least (1, 1).
    /// </summary>
    public static ContentExtent Measure(TerminalContent? content)
    {
        var maxRow = -1;
        var maxCols = 0;

        var grid = content?.Cells;
        if (grid != null)
        {
            for (var row = 0; row < grid.Length; row++)
            {
                var cells = grid[row];

                var runningCol = 0;
                var lastColInRow = 0;
                var rowHasContent = false;

                foreach (var cell in cells)
                {
                    if (!ContentAnalysis.IsBlankCell(cell))
                    {
                        rowHasContent = true;
                        // Rightmost column the glyph occupies (cells span at least one column).
                        lastColInRow = runningCol + Math.Max(cell.Width, 1);
                    }

                    runningCol += cell.Width;
                }

                if (rowHasContent)
                {
                    maxRow = row;
                    if (lastColInRow > maxCols)
                        maxCols = lastColInRow;
                }
            }
        }

        return new ContentExtent(Math.Max(maxCols, 1), Math.Max(maxRow + 1, 1));
    }

    /// <summary>
    /// Measures the union extent across many terminal states - a row or column that is
    /// non-blank in any state is retained, so an animated SVG cropped to this extent
    /// never clips a row/column that appears partway through the recording.
    /// Always returns at least (1, 1).
    /// </summary>
    public static ContentExtent Union(IEnumerable<TerminalContent>? states)
    {
        var cols = 1;
        var rows = 1;

        if (states != null)
        {
            foreach (var state in states)
            {
                var extent = Measure(state);
                if (extent.Cols > cols) cols = extent.Cols;
                if (extent.Rows > rows) rows = extent.Rows;
            }
        }

        return new ContentExtent(cols, rows);
    }
}
