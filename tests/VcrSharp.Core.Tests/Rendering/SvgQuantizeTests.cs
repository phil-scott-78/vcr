using Shouldly;
using VcrSharp.Core.Rendering;
using VcrSharp.Infrastructure.Rendering;

namespace VcrSharp.Core.Tests.Rendering;

/// <summary>
/// Tests for <see cref="SvgWriter.QuantizeToFramerate"/>, the framerate down-sampler for animated SVG.
/// The critical contract is that when a settled frame is preceded by a short-lived transient inside the
/// same frame window (the event-driven capture's mid-redraw tear), the SETTLED frame is the one kept —
/// otherwise the torn intermediate is frozen for the whole display slot, producing visible corruption.
/// </summary>
public class SvgQuantizeTests
{
    private static TerminalStateWithTime State(double ts, string tag = "x")
    {
        // Content identity doesn't matter for quantization (it keys purely on timestamps); the tag just
        // lets a test assert which frame survived.
        var cells = new[] { new[] { new TerminalCell { Character = tag, Width = 1 } } };
        return new TerminalStateWithTime
        {
            Content = new TerminalContent { Cols = 1, Rows = 1, Cells = cells },
            TimestampSeconds = ts,
        };
    }

    private static string Tag(TerminalStateWithTime s) => s.Content.Cells[0][0].Character!;

    [Fact]
    public void TransientThenSettled_InSameWindow_KeepsSettled()
    {
        // Mirrors the real capture stream: each ~2 s plateau is a short transient tear immediately
        // followed (here ~14 ms later, inside one 1/50 s = 20 ms window) by the settled frame.
        const int fps = 50;
        var states = new List<TerminalStateWithTime>
        {
            State(0.000, "settled0"),
            State(2.000, "torn1"), State(2.014, "settled1"),
            State(4.000, "torn2"), State(4.013, "settled2"),
            State(6.000, "torn3"), State(6.015, "settled3"),
        };

        var kept = SvgWriter.QuantizeToFramerate(states, fps).Select(Tag).ToList();

        // The torn intermediates are dropped; the settled frame of each plateau survives.
        kept.ShouldBe(new[] { "settled0", "settled1", "settled2", "settled3" });
    }

    [Fact]
    public void FinalSettledFrame_IsAlwaysKept()
    {
        // The end-of-recording settled frame must survive even when it trails a transient within a window.
        var states = new List<TerminalStateWithTime>
        {
            State(0.000, "a"),
            State(5.000, "torn"), State(5.012, "final"),
        };

        SvgWriter.QuantizeToFramerate(states, 50).Select(Tag).Last().ShouldBe("final");
    }

    [Fact]
    public void ContinuousFastStream_IsDownsampledTowardFramerate()
    {
        // A progress-bar-style emitter that changes every ~16 ms over 2 s should be thinned to roughly
        // the target framerate (one frame per 1/fps window), not collapsed and not left untouched.
        const int fps = 25; // 40 ms window
        var states = new List<TerminalStateWithTime>();
        for (var i = 0; i <= 120; i++) // 0..1.92 s in 16 ms steps => 121 states
            states.Add(State(i * 0.016, $"f{i}"));

        var kept = SvgWriter.QuantizeToFramerate(states, fps);

        // ~2 s at 25 fps ≈ 50 frames; allow slack but it must be a real reduction from 121 and not a collapse.
        kept.Count.ShouldBeGreaterThan(30);
        kept.Count.ShouldBeLessThan(70);
        // Timestamps stay strictly increasing and the endpoints are preserved.
        Tag(kept[0]).ShouldBe("f0");
        Tag(kept[^1]).ShouldBe("f120");
    }

    [Fact]
    public void TwoOrFewerStates_ReturnedUnchanged()
    {
        var states = new List<TerminalStateWithTime> { State(0.0, "a"), State(1.0, "b") };
        SvgWriter.QuantizeToFramerate(states, 50).ShouldBeSameAs(states);
    }
}
