using VcrSharp.Core.Rendering;
using VcrSharp.Core.Session;

namespace VcrSharp.Infrastructure.Rendering;

/// <summary>
/// Turns a raw stream of captured grid snapshots (from the native poll loop) into an animated SVG via
/// the existing <see cref="SvgRenderer"/>: drops leading-blank frames and a trailing static tail so a
/// loop starts on content, rebaselines timestamps to zero, and stretches the duration to the true end
/// so the final frame holds.
/// </summary>
public static class NativeSvgWriter
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
    /// by keeping the first state in each 1/fps window and skipping everything until the window elapses.
    /// The final state is always kept so the settled end-of-recording output is never dropped. Returns
    /// the input unchanged when there is nothing to thin (fps &lt;= 0 or two-or-fewer states).
    /// </summary>
    internal static List<TerminalStateWithTime> QuantizeToFramerate(List<TerminalStateWithTime> states, int fps)
    {
        if (fps <= 0 || states.Count <= 2) return states;

        var minInterval = 1.0 / fps;
        var result = new List<TerminalStateWithTime>(states.Count);
        var lastKept = double.NegativeInfinity;

        for (var i = 0; i < states.Count; i++)
        {
            var isLast = i == states.Count - 1;
            if (isLast || states[i].TimestampSeconds - lastKept >= minInterval)
            {
                result.Add(states[i]);
                lastKept = states[i].TimestampSeconds;
            }
        }

        return result;
    }
}
