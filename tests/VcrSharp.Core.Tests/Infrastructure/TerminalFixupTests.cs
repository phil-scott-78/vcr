using Shouldly;
using VcrSharp.Core.Rendering;
using VcrSharp.Infrastructure.Terminal;

namespace VcrSharp.Core.Tests.Infrastructure;

/// <summary>
/// Regression tests for two bugs surfaced by the adversarial audit:
/// (1) <see cref="KeyMap.ForCombination"/> silently dropped Ctrl/Alt/Shift modifiers for any key
///     whose byte sequence was longer than one char (arrows, Home/End, Delete, function keys);
/// (2) the frame-dedup <see cref="TerminalRenderer.Signature"/> omitted several attributes the
///     renderers actually draw (strike/overline/dim/conceal/underline-style), so frames that differed
///     ONLY in those attributes were collapsed and dropped.
/// </summary>
public class TerminalFixupTests
{
    private const string E = "\x1b";

    [Fact]
    public void ForCombination_ModifiedSpecialKeys_EmitXtermCsiParameters()
    {
        // mod = 1 + Shift(1) + Alt(2) + Ctrl(4)
        KeyMap.ForCombination(["Control"], "ArrowRight").ShouldBe($"{E}[1;5C");
        KeyMap.ForCombination(["Control"], "ArrowLeft").ShouldBe($"{E}[1;5D");
        KeyMap.ForCombination(["Alt"], "ArrowUp").ShouldBe($"{E}[1;3A");
        KeyMap.ForCombination(["Shift"], "Home").ShouldBe($"{E}[1;2H");
        KeyMap.ForCombination(["Control"], "End").ShouldBe($"{E}[1;5F");
        KeyMap.ForCombination(["Control"], "Delete").ShouldBe($"{E}[3;5~");
        KeyMap.ForCombination(["Control", "Shift"], "F5").ShouldBe($"{E}[15;6~");
        KeyMap.ForCombination(["Control"], "F1").ShouldBe($"{E}[1;5P");
    }

    [Fact]
    public void ForCombination_PlainKeys_KeepControlFoldingAndBackTab()
    {
        KeyMap.ForCombination(["Control"], "c").ShouldBe("\x03"); // Ctrl+C → 0x03
        KeyMap.ForCombination(["Shift"], "Tab").ShouldBe($"{E}[Z"); // back-tab (CBT)
        KeyMap.ForCombination(["Alt"], "a").ShouldBe($"{E}a");      // Alt prefixes ESC
    }

    [Fact]
    public void Signature_DistinguishesEveryRenderedAttribute()
    {
        var baseline = TerminalRenderer.Signature(OneCell(_ => { }));

        TerminalRenderer.Signature(OneCell(c => c.IsStrikethrough = true)).ShouldNotBe(baseline);
        TerminalRenderer.Signature(OneCell(c => c.IsOverline = true)).ShouldNotBe(baseline);
        TerminalRenderer.Signature(OneCell(c => c.IsDim = true)).ShouldNotBe(baseline);
        TerminalRenderer.Signature(OneCell(c => c.IsConceal = true)).ShouldNotBe(baseline);
        TerminalRenderer.Signature(OneCell(c => c.IsBlink = true)).ShouldNotBe(baseline);
        TerminalRenderer.Signature(OneCell(c => c.UnderlineStyle = 3)).ShouldNotBe(baseline);
        TerminalRenderer.Signature(OneCell(c => c.UnderlineColor = "#ff0000")).ShouldNotBe(baseline);
    }

    private static TerminalContent OneCell(Action<TerminalCell> mutate)
    {
        var cell = new TerminalCell { Character = "X" };
        mutate(cell);
        return new TerminalContent
        {
            Cols = 1,
            Rows = 1,
            Cells = [[cell]],
            CursorX = 0,
            CursorY = 0,
            CursorVisible = false,
        };
    }
}
