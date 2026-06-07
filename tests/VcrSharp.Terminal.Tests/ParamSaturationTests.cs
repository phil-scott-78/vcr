using Shouldly;
using VcrSharp.Core.Rendering;
using VcrSharp.Terminal;

namespace VcrSharp.Terminal.Tests;

/// <summary>
/// Regression for the CSI numeric-parameter accumulator: a 10+ digit parameter used to overflow int
/// (the <c>_cur * 10</c> happened BEFORE the saturating <c>Math.Min</c> cap), wrapping to a negative value
/// that <c>Param()</c>/<c>Val()</c> then read as omitted/zero. So a deliberately-huge "go to the far edge"
/// count silently collapsed to 1 (or home) and rendered the wrong screen. The fix saturates before the
/// multiply, so a huge param clamps to the grid edge as a real terminal does.
/// </summary>
public class ParamSaturationTests
{
    private const string E = "\x1b";

    private static TerminalContent Render(int cols, int rows, string input)
    {
        var s = new VtScreen(cols, rows);
        s.Feed(input);
        return s.ToTerminalContent();
    }

    [Fact]
    public void HugeVpaParam_ClampsToLastRow_NotHome()
    {
        // VPA with ten 9s: pre-fix this overflowed negative and homed to row 0.
        Render(80, 24, E + "[9999999999d").CursorY.ShouldBe(23);
    }

    [Fact]
    public void HugeCursorDownParam_ClampsToBottom()
    {
        Render(80, 24, E + "[9999999999B").CursorY.ShouldBe(23);
    }

    [Fact]
    public void HugeCursorForwardParam_ClampsToLastColumn()
    {
        Render(80, 24, E + "[9999999999C").CursorX.ShouldBe(79);
    }

    [Fact]
    public void HugeCupParam_ClampsToBottomRight()
    {
        var c = Render(80, 24, E + "[9999999999;9999999999H");
        c.CursorY.ShouldBe(23);
        c.CursorX.ShouldBe(79);
    }
}
