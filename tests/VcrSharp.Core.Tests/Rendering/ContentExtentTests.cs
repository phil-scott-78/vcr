using Shouldly;
using VcrSharp.Core.Rendering;

namespace VcrSharp.Core.Tests.Rendering;

/// <summary>
/// Tests for ContentExtent measurement and the shared blank-cell predicate.
/// </summary>
public class ContentExtentTests
{
    private static TerminalCell Cell(string ch, int width = 1, string? bg = null) =>
        new() { Character = ch, Width = width, BackgroundColor = bg };

    /// <summary>Builds a cols x rows grid of spaces, overlaying the given lines from the top-left.</summary>
    private static TerminalContent Grid(int cols, int rows, params string[] lines)
    {
        var cells = new TerminalCell[rows][];
        for (var r = 0; r < rows; r++)
        {
            var row = new TerminalCell[cols];
            var line = r < lines.Length ? lines[r] : string.Empty;
            for (var c = 0; c < cols; c++)
            {
                var ch = c < line.Length ? line[c].ToString() : " ";
                row[c] = Cell(ch);
            }
            cells[r] = row;
        }
        return new TerminalContent { Cols = cols, Rows = rows, Cells = cells };
    }

    [Fact]
    public void Measure_AllBlank_ClampsToOneByOne()
    {
        var content = Grid(80, 24);
        var extent = ContentExtent.Measure(content);
        extent.ShouldBe(new ContentExtent(1, 1));
    }

    [Fact]
    public void Measure_SingleWord_ReturnsWordWidthAndOneRow()
    {
        var content = Grid(80, 24, "hi");
        var extent = ContentExtent.Measure(content);
        extent.Cols.ShouldBe(2);
        extent.Rows.ShouldBe(1);
    }

    [Fact]
    public void Measure_TrimsTrailingBlankRows()
    {
        // Content on rows 0,1,2; rows 3..23 blank.
        var content = Grid(80, 24, "row0", "row1", "row2");
        var extent = ContentExtent.Measure(content);
        extent.Rows.ShouldBe(3);
        extent.Cols.ShouldBe(4); // "row0"
    }

    [Fact]
    public void Measure_TrimsRightSideBlankColumns()
    {
        var content = Grid(80, 1, "hello");
        var extent = ContentExtent.Measure(content);
        extent.Cols.ShouldBe(5);
        extent.Rows.ShouldBe(1);
    }

    [Fact]
    public void Measure_BackgroundColoredSpace_CountsAsContent()
    {
        // A row of spaces, but one space cell carries a background color.
        var row = new TerminalCell[10];
        for (var c = 0; c < 10; c++) row[c] = Cell(" ");
        row[3] = Cell(" ", bg: "#ff0000");
        var content = new TerminalContent { Cols = 10, Rows = 1, Cells = new[] { row } };

        var extent = ContentExtent.Measure(content);
        extent.Rows.ShouldBe(1);
        extent.Cols.ShouldBe(4); // up to and including column index 3
    }

    [Fact]
    public void Measure_WideChar_AdvancesColumnsByTwo()
    {
        // "A" then a wide char (width 2) + its continuation (width 0). Visual width = 3 columns.
        var row = new[]
        {
            Cell("A"),
            Cell("世", width: 2),
            Cell(" ", width: 0),
            Cell(" "),
            Cell(" "),
        };
        var content = new TerminalContent { Cols = 5, Rows = 1, Cells = new[] { row } };

        var extent = ContentExtent.Measure(content);
        extent.Cols.ShouldBe(3);
        extent.Rows.ShouldBe(1);
    }

    [Fact]
    public void Union_PicksMaxRowsAndColsAcrossStates()
    {
        var frameA = Grid(80, 24, "hello");          // 5 cols, 1 row
        var frameB = Grid(80, 24, "", "", "", "", "", "wider-line"); // row index 5
        var union = ContentExtent.Union(new[] { frameA, frameB });
        union.Rows.ShouldBe(6);          // frameB reaches row index 5
        union.Cols.ShouldBe(10);         // "wider-line"
    }

    [Fact]
    public void IsBlankRow_MatchesSvgRendererDefinition()
    {
        var blank = new[] { Cell(" "), Cell(" ") };
        ContentAnalysis.IsBlankRow(blank).ShouldBeTrue();

        var withText = new[] { Cell(" "), Cell("x") };
        ContentAnalysis.IsBlankRow(withText).ShouldBeFalse();

        var withBg = new[] { Cell(" "), Cell(" ", bg: "#000000") };
        ContentAnalysis.IsBlankRow(withBg).ShouldBeFalse();
    }

    [Fact]
    public void TrimBlankLoopRange_DropsLeadingBlankFrames()
    {
        var blank = Grid(10, 1);
        var hi = Grid(10, 1, "hi");
        var (start, end) = ContentAnalysis.TrimBlankLoopRange(new[] { blank, blank, hi, hi });
        start.ShouldBe(2);
        end.ShouldBe(3);
    }

    [Fact]
    public void TrimBlankLoopRange_CollapsesTrailingDuplicatesToFirstOccurrence()
    {
        var a = Grid(10, 1, "a");
        var b = Grid(10, 1, "b");
        var (start, end) = ContentAnalysis.TrimBlankLoopRange(new[] { a, b, b, b });
        start.ShouldBe(0);
        end.ShouldBe(1); // keeps "a" and the first "b"
    }

    [Fact]
    public void TrimBlankLoopRange_NoBlankOrDuplicates_IsNoOp()
    {
        var a = Grid(10, 1, "a");
        var b = Grid(10, 1, "b");
        var (start, end) = ContentAnalysis.TrimBlankLoopRange(new[] { a, b });
        start.ShouldBe(0);
        end.ShouldBe(1);
    }
}
