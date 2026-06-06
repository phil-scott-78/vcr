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
}
