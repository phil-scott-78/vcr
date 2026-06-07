using Shouldly;
using VcrSharp.Core.Rendering;
using VcrSharp.Infrastructure.Terminal;

namespace VcrSharp.Core.Tests.Infrastructure;

/// <summary>
/// Regression tests for two native-path bugs surfaced by the adversarial audit:
/// (1) <see cref="NativeKeyMap.ForCombination"/> silently dropped Ctrl/Alt/Shift modifiers for any key
///     whose byte sequence was longer than one char (arrows, Home/End, Delete, function keys);
/// (2) the frame-dedup <see cref="NativeTerminalRenderer.Signature"/> omitted several attributes the
///     renderers actually draw (strike/overline/dim/conceal/underline-style), so frames that differed
///     ONLY in those attributes were collapsed and dropped.
/// </summary>
public class NativeFixupTests
{
    private const string E = "\x1b";

    [Fact]
    public void ForCombination_ModifiedSpecialKeys_EmitXtermCsiParameters()
    {
        // mod = 1 + Shift(1) + Alt(2) + Ctrl(4)
        NativeKeyMap.ForCombination(["Control"], "ArrowRight").ShouldBe($"{E}[1;5C");
        NativeKeyMap.ForCombination(["Control"], "ArrowLeft").ShouldBe($"{E}[1;5D");
        NativeKeyMap.ForCombination(["Alt"], "ArrowUp").ShouldBe($"{E}[1;3A");
        NativeKeyMap.ForCombination(["Shift"], "Home").ShouldBe($"{E}[1;2H");
        NativeKeyMap.ForCombination(["Control"], "End").ShouldBe($"{E}[1;5F");
        NativeKeyMap.ForCombination(["Control"], "Delete").ShouldBe($"{E}[3;5~");
        NativeKeyMap.ForCombination(["Control", "Shift"], "F5").ShouldBe($"{E}[15;6~");
        NativeKeyMap.ForCombination(["Control"], "F1").ShouldBe($"{E}[1;5P");
    }

    [Fact]
    public void ForCombination_PlainKeys_KeepControlFoldingAndBackTab()
    {
        NativeKeyMap.ForCombination(["Control"], "c").ShouldBe("\x03"); // Ctrl+C → 0x03
        NativeKeyMap.ForCombination(["Shift"], "Tab").ShouldBe($"{E}[Z"); // back-tab (CBT)
        NativeKeyMap.ForCombination(["Alt"], "a").ShouldBe($"{E}a");      // Alt prefixes ESC
    }

    [Fact]
    public void Signature_DistinguishesEveryRenderedAttribute()
    {
        var baseline = NativeTerminalRenderer.Signature(OneCell(_ => { }));

        NativeTerminalRenderer.Signature(OneCell(c => c.IsStrikethrough = true)).ShouldNotBe(baseline);
        NativeTerminalRenderer.Signature(OneCell(c => c.IsOverline = true)).ShouldNotBe(baseline);
        NativeTerminalRenderer.Signature(OneCell(c => c.IsDim = true)).ShouldNotBe(baseline);
        NativeTerminalRenderer.Signature(OneCell(c => c.IsConceal = true)).ShouldNotBe(baseline);
        NativeTerminalRenderer.Signature(OneCell(c => c.IsBlink = true)).ShouldNotBe(baseline);
        NativeTerminalRenderer.Signature(OneCell(c => c.UnderlineStyle = 3)).ShouldNotBe(baseline);
        NativeTerminalRenderer.Signature(OneCell(c => c.UnderlineColor = "#ff0000")).ShouldNotBe(baseline);
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
