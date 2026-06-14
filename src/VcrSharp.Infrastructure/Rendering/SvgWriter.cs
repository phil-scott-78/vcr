using VcrSharp.Core.Rendering;
using VcrSharp.Core.Session;

namespace VcrSharp.Infrastructure.Rendering;

/// <summary>
/// Turns a raw stream of captured grid snapshots into an animated SVG via
/// the <see cref="SvgRenderer"/>: drops leading-blank frames and a trailing static tail so a
/// loop starts on content, rebaselines timestamps to zero, and stretches the duration to the true end
/// so the final frame holds.
/// </summary>
public static class SvgWriter
{
    /// <summary>Writes the animated SVG and returns the number of frames emitted (0 if there was nothing to render).</summary>
    public static async Task<int> WriteAnimatedAsync(IReadOnlyList<TerminalStateWithTime> raw,
        double totalSeconds, SessionOptions options, string outputPath, CancellationToken cancellationToken = default)
    {
        if (raw.Count == 0) return 0;

        var (keepStart, keepEnd) = ContentAnalysis.TrimBlankLoopRange(raw.Select(s => s.Content).ToList());
        var kept = raw.Skip(keepStart).Take(keepEnd - keepStart + 1).ToList();
        if (kept.Count == 0) return 0;

        var baseline = kept[0].TimestampSeconds;
        var states = kept
            .Select(s => new TerminalStateWithTime { Content = s.Content, TimestampSeconds = s.TimestampSeconds - baseline })
            .ToList();

        // Thin the captured states down to the configured framerate. Capture is uncapped event-driven
        // (a frame per distinct grid state), which for fast emitters like progress bars yields far more
        // distinct states than an SVG needs — and SVG file size scales with the number of distinct
        // row-states. Sampling at Framerate fps coalesces sub-frame-interval states with no visible loss
        // (the final settled frame is always kept so end values survive).
        states = QuantizeToFramerate(states, options.Framerate);

        var totalDuration = Math.Max(states[^1].TimestampSeconds, totalSeconds - baseline);

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var renderer = new SvgRenderer(options);
        if (options.FitToContent)
        {
            var extent = ContentExtent.Union(states.Select(s => s.Content));
            renderer.SetContentExtent(extent.Cols, extent.Rows);
        }
        await renderer.RenderAnimatedAsync(outputPath, states, totalDuration, cancellationToken);
        return states.Count;
    }

    /// <summary>
    /// Down-samples a timestamp-ordered state stream to at most <paramref name="fps"/> frames per second
    /// by keeping the LAST state in each 1/fps window (plus the first and final states). Returns the
    /// input unchanged when there is nothing to thin (fps &lt;= 0 or two-or-fewer states).
    /// <para>
    /// Keeping the last — not the first — state in each window is what makes this safe for the
    /// event-driven capture stream. A screen redraw (a table scrolling in, a TUI repaint) is captured
    /// as a short-lived torn intermediate frame immediately followed by the settled frame, often only
    /// ~10&#8211;15&#160;ms apart. Both fall inside one 1/fps window. Keeping the first would freeze the
    /// torn frame for the whole window (~one display slot, e.g. a 2&#160;s plateau) — visible corruption
    /// where rows from two different screens overlap. Keeping the last drops the transient and shows the
    /// settled screen, which is also what the un-quantized raster/GIF path effectively displays (the tear
    /// only ever flashes for its true sub-frame duration there). The final settled frame is always kept so
    /// end-of-recording output survives.
    /// </para>
    /// </summary>
    internal static List<TerminalStateWithTime> QuantizeToFramerate(List<TerminalStateWithTime> states, int fps)
    {
        if (fps <= 0 || states.Count <= 2) return states;

        var minInterval = 1.0 / fps;
        var result = new List<TerminalStateWithTime>(states.Count);

        static long Window(double ts, double minInterval) => (long)(ts / minInterval);

        for (var i = 0; i < states.Count; i++)
        {
            // Keep the first state (so t=0 already shows content) and the last state (settled end).
            // Otherwise keep a state only when it is the last one in its frame window — i.e. the next
            // state belongs to a later window. A state superseded within the same window is a transient
            // mid-redraw frame and is skipped in favor of the settled state it resolves into.
            var isFirst = i == 0;
            var isLast = i == states.Count - 1;
            var lastInWindow = isLast ||
                Window(states[i + 1].TimestampSeconds, minInterval) > Window(states[i].TimestampSeconds, minInterval);

            if (isFirst || lastInWindow)
                result.Add(states[i]);
        }

        return result;
    }
}
