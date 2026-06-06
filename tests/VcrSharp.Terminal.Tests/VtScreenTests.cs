using Shouldly;
using VcrSharp.Core.Rendering;
using VcrSharp.Terminal;

namespace VcrSharp.Terminal.Tests;

/// <summary>Tests for the VT/ANSI parser that backs the browserless render path.</summary>
public class VtScreenTests
{
    private const string E = ""; // ESC

    private static TerminalContent Render(int cols, int rows, string input)
    {
        var screen = new VtScreen(cols, rows);
        screen.Feed(input);
        return screen.ToTerminalContent();
    }

    private static string RowText(TerminalContent c, int row) =>
        string.Concat(c.Cells[row].Select(cell => cell.Character.Length == 0 ? "" : cell.Character)).TrimEnd();

    [Fact]
    public void PlainText_WritesCells()
    {
        var c = Render(10, 1, "hello");
        RowText(c, 0).ShouldBe("hello");
        c.Cells[0][0].Character.ShouldBe("h");
        c.Cells[0][4].Character.ShouldBe("o");
        c.Cells[0][5].Character.ShouldBe(" "); // untouched
    }

    [Fact]
    public void CarriageReturnAndLineFeed_MoveToNextRow()
    {
        var c = Render(5, 3, "ab\r\ncd");
        RowText(c, 0).ShouldBe("ab");
        RowText(c, 1).ShouldBe("cd");
    }

    [Fact]
    public void Sgr_BasicForeground_SetsPaletteIndexString()
    {
        var c = Render(5, 1, $"{E}[32mX{E}[0mY");
        c.Cells[0][0].Character.ShouldBe("X");
        c.Cells[0][0].ForegroundColor.ShouldBe("2");   // green palette index
        c.Cells[0][1].Character.ShouldBe("Y");
        c.Cells[0][1].ForegroundColor.ShouldBeNull();  // reset to default
    }

    [Fact]
    public void Sgr_BrightForeground_MapsTo8Through15()
    {
        Render(2, 1, $"{E}[92mA").Cells[0][0].ForegroundColor.ShouldBe("10"); // bright green
    }

    [Fact]
    public void Sgr_Background_SetsPaletteIndex()
    {
        Render(2, 1, $"{E}[44mB").Cells[0][0].BackgroundColor.ShouldBe("4"); // blue bg
    }

    [Fact]
    public void Sgr_BoldItalicUnderline_SetFlags()
    {
        var cell = Render(2, 1, $"{E}[1;3;4mA").Cells[0][0];
        cell.IsBold.ShouldBeTrue();
        cell.IsItalic.ShouldBeTrue();
        cell.IsUnderline.ShouldBeTrue();
    }

    [Fact]
    public void Sgr_256Color_SetsIndexString()
    {
        Render(2, 1, $"{E}[38;5;208mA").Cells[0][0].ForegroundColor.ShouldBe("208");
    }

    [Fact]
    public void Sgr_Truecolor_SetsHexString()
    {
        var cell = Render(2, 1, $"{E}[1;38;2;255;0;0mZ").Cells[0][0];
        cell.ForegroundColor.ShouldBe("#ff0000");
        cell.IsBold.ShouldBeTrue();
    }

    [Fact]
    public void Truecolor_Background_SetsHexString()
    {
        Render(2, 1, $"{E}[48;2;16;32;48mB").Cells[0][0].BackgroundColor.ShouldBe("#102030");
    }

    [Fact]
    public void Cup_PositionsCursorOneBased()
    {
        var c = Render(5, 5, $"{E}[2;3HX"); // row 2, col 3 (1-based)
        c.Cells[1][2].Character.ShouldBe("X");
    }

    [Fact]
    public void EraseInLine_ClearsToEnd()
    {
        // write abc, jump to col 1 (CHA), erase to end, write Z
        var c = Render(5, 1, $"abc{E}[1G{E}[KZ");
        RowText(c, 0).ShouldBe("Z");
    }

    [Fact]
    public void EraseInDisplay_All_ClearsScreen()
    {
        var c = Render(5, 2, $"abc\r\ndef{E}[2J");
        RowText(c, 0).ShouldBe("");
        RowText(c, 1).ShouldBe("");
    }

    [Fact]
    public void Autowrap_DeferredAtRightMargin()
    {
        var c = Render(3, 2, "abcd");
        RowText(c, 0).ShouldBe("abc");
        RowText(c, 1).ShouldBe("d");
    }

    [Fact]
    public void LineFeed_AtBottom_ScrollsUp()
    {
        var c = Render(3, 2, "a\r\nb\r\nc");
        RowText(c, 0).ShouldBe("b");
        RowText(c, 1).ShouldBe("c");
    }

    [Fact]
    public void WideChar_OccupiesTwoCells()
    {
        var c = Render(5, 1, "世"); // CJK '世' (wide)
        c.Cells[0][0].Width.ShouldBe(2);
        c.Cells[0][1].Width.ShouldBe(0); // continuation
        c.Cells[0][0].Character.ShouldBe("世");
    }

    [Fact]
    public void UnknownPrivateModes_AreIgnored_NotPrinted()
    {
        // DEC private mode set/reset (hide/show cursor) must not leak into the grid.
        var c = Render(5, 1, $"{E}[?25lAB{E}[?25h");
        RowText(c, 0).ShouldBe("AB");
    }

    [Fact]
    public void OscTitle_BelTerminated_DoesNotLeakIntoGrid()
    {
        // ConPTY emits OSC 0 (window title) ending in BEL — the title text must not be printed.
        var c = Render(40, 1, $"AB{E}]0;C:\\Program Files\\pwsh.exeCD");
        RowText(c, 0).ShouldBe("ABCD");
    }

    [Fact]
    public void OscTitle_StTerminated_DoesNotLeakIntoGrid()
    {
        // OSC terminated by ST (ESC backslash) instead of BEL.
        var c = Render(40, 1, $"AB{E}]2;title{E}\\CD");
        RowText(c, 0).ShouldBe("ABCD");
    }

    [Fact]
    public void CharsetDesignator_IsConsumed_NotPrinted()
    {
        // ESC ( B (designate US-ASCII) must consume the 'B', not print it.
        var c = Render(5, 1, $"{E}(BX");
        RowText(c, 0).ShouldBe("X");
    }

    [Fact]
    public void EraseChars_BlanksInPlace_WithoutMovingCursor()
    {
        // ECH: write ABCDE, go to col 3, erase 2 chars -> "AB  E". (Spectre relies on this.)
        var c = Render(5, 1, $"ABCDE{E}[3G{E}[2X");
        c.Cells[0][0].Character.ShouldBe("A");
        c.Cells[0][1].Character.ShouldBe("B");
        c.Cells[0][2].Character.ShouldBe(" ");
        c.Cells[0][3].Character.ShouldBe(" ");
        c.Cells[0][4].Character.ShouldBe("E");
    }

    [Fact]
    public void DeleteChars_ShiftsLeft()
    {
        // DCH: ABCDE, home, delete 2 -> "CDE".
        var c = Render(5, 1, $"ABCDE{E}[H{E}[2P");
        RowText(c, 0).ShouldBe("CDE");
    }
}
