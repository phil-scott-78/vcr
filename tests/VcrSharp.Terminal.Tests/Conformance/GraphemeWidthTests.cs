using Shouldly;
using VcrSharp.Core.Rendering;

namespace VcrSharp.Terminal.Tests.Conformance;

/// <summary>
/// Grapheme-cluster width: a base character plus its combining marks / emoji modifiers / ZWJ joins /
/// variation selectors must occupy a single cell of the BASE width, so a following marker lands in the
/// column a real terminal would put it. If the engine over-counts (e.g. a skin-tone modifier as its own
/// width-2 cell), everything after it drifts and the grid diverges from a real terminal.
/// </summary>
public sealed class GraphemeWidthTests
{
    private static int ColOf(TerminalContent c, string marker)
    {
        for (var col = 0; col < c.Cols; col++)
            if (c.Cells[0][col].Character == marker) return col;
        return -1;
    }

    private static TerminalContent Render(string input)
    {
        var s = new VtScreen(20, 1);
        s.Feed(input);
        return s.ToTerminalContent();
    }

    [Fact]
    public void StackedDiacritics_OccupyOneColumn()
    {
        // 'e' + combining acute + combining circumflex → one width-1 cell; 'Z' lands at column 1.
        var input = "e" + (char)0x0301 + (char)0x0302 + "Z";
        ColOf(Render(input), "Z").ShouldBe(1);
    }

    [Fact]
    public void SkinToneModifier_KeepsBaseWidthTwo()
    {
        // 👍 (U+1F44D, wide) + skin-tone modifier (U+1F3FD) → one width-2 cell; 'Z' lands at column 2.
        var input = char.ConvertFromUtf32(0x1F44D) + char.ConvertFromUtf32(0x1F3FD) + "Z";
        ColOf(Render(input), "Z").ShouldBe(2);
    }

    [Fact]
    public void ZwjEmojiSequence_OccupiesOneGrapheme()
    {
        // 👨 + ZWJ + 👩 + ZWJ + 👧 (family) → one width-2 grapheme; 'Z' lands at column 2.
        var zwj = ((char)0x200D).ToString();
        var input = char.ConvertFromUtf32(0x1F468) + zwj + char.ConvertFromUtf32(0x1F469) + zwj +
                    char.ConvertFromUtf32(0x1F467) + "Z";
        ColOf(Render(input), "Z").ShouldBe(2);
    }

    [Fact]
    public void VariationSelector_IsZeroWidth()
    {
        // '#' + VS16 (U+FE0F, emoji presentation) is a single grapheme; the selector adds no column.
        var input = "#" + (char)0xFE0F + "Z";
        ColOf(Render(input), "Z").ShouldBe(1);
    }
}
