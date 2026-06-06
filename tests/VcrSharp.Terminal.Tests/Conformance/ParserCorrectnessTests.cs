using Shouldly;
using VcrSharp.Core.Rendering;

namespace VcrSharp.Terminal.Tests.Conformance;

/// <summary>
/// Precise acceptance criteria for the known divergences the audit identified — each asserts the
/// CORRECT target behavior and is <c>Skip</c>ped with the phase that will deliver it. They are the
/// executable checklist for the build: un-skip each as its phase lands (it should then go green).
/// See docs/vt-engine-conformance.md.
/// </summary>
public sealed class ParserCorrectnessTests
{
    private const string Skip_P1 = "P1 (Williams parser rewrite) — see docs/vt-engine-conformance.md";
    private const string Skip_P2 = "P2 (scroll region + edit ops) — see docs/vt-engine-conformance.md";
    private const string Skip_P4 = "P4 (DEC modes + alt screen) — see docs/vt-engine-conformance.md";

    private static readonly string E = ((char)0x1b).ToString();

    private static TerminalContent Render(int cols, int rows, string input)
    {
        var s = new VtScreen(cols, rows);
        s.Feed(input);
        return s.ToTerminalContent();
    }

    private static string Row(TerminalContent c, int row) =>
        string.Concat(c.Cells[row].Select(cell => cell.Character.Length == 0 ? "" : cell.Character)).TrimEnd();

    [Fact(Skip = Skip_P1)]
    public void OscFollowedByCsi_AppliesTheCsi()
    {
        // BUG today: after the OSC's terminating ESC, the parser drops the next byte ('['), so "31m"
        // prints as text and X is left default. Williams: ESC re-enters escape state → CSI applies.
        var c = Render(10, 1, $"{E}]0;title{E}[31mX");
        c.Cells[0][0].Character.ShouldBe("X");
        c.Cells[0][0].ForegroundColor.ShouldBe("1"); // red
    }

    [Fact(Skip = Skip_P1)]
    public void ColonSubParams_TrueColor_Applies()
    {
        // BUG today: ':' (0x3A) is unhandled in CSI param state → the sequence aborts and prints as text.
        var c = Render(10, 1, $"{E}[38:2::255:0:0mX");
        c.Cells[0][0].Character.ShouldBe("X");
        c.Cells[0][0].ForegroundColor.ShouldBe("#ff0000");
    }

    [Fact(Skip = Skip_P2)]
    public void ScrollRegion_ConstrainsLineFeedScroll()
    {
        // DECSTBM rows 1..2 (1-based). With the region active, the LF at the bottom margin scrolls only
        // rows 0..1, so "L2" ends up on row 0. Today there is no scroll region → row 0 stays "L1".
        var c = Render(10, 4, $"{E}[1;2r{E}[HL1\nL2\nL3");
        Row(c, 0).ShouldBe("L2");
        Row(c, 1).ShouldBe("L3");
    }

    [Fact(Skip = Skip_P2)]
    public void InsertLine_PushesRowsDown()
    {
        // IL ("CSI L") at home inserts a blank line, pushing "A" down to row 1. Today 'L' is a no-op.
        var c = Render(10, 4, $"A\r\nB\r\nC{E}[H{E}[L");
        Row(c, 0).ShouldBe("");
        Row(c, 1).ShouldBe("A");
    }

    [Fact(Skip = Skip_P4)]
    public void AltScreen_StartsBlank()
    {
        // Entering the alternate buffer (DECSET 1049) shows a blank screen; the main buffer's "P" is
        // hidden until 1049l restores it. Today the mode is ignored, so "P" stays visible.
        var c = Render(10, 1, $"P{E}[?1049h");
        Row(c, 0).ShouldBe("");
    }

    [Fact(Skip = Skip_P4)]
    public void HideCursor_ReflectedInSnapshot()
    {
        // DECTCEM (CSI ?25l) hides the cursor; today ToTerminalContent hardcodes CursorVisible=false,
        // so it can never report a *visible* cursor either. After P4, default is visible and ?25l hides.
        var shown = Render(10, 1, "X");
        shown.CursorVisible.ShouldBeTrue();
        var hidden = Render(10, 1, $"X{E}[?25l");
        hidden.CursorVisible.ShouldBeFalse();
    }
}
