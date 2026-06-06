using VcrSharp.Core.Recording;
using VcrSharp.Core.Rendering;
using VcrSharp.Core.Session;
using VcrSharp.Infrastructure.Recording;

namespace VcrSharp.Infrastructure.Rendering.Encoders;

/// <summary>
/// SVG encoder using SMIL animations for partial screen updates.
/// 1. Diffs consecutive frames to identify changed rows
/// 2. Renders each unique row state once
/// 3. Uses SMIL &lt;animate&gt; elements to toggle visibility at appropriate times
/// 4. Animates cursor position with SMIL &lt;animate&gt;
///
/// This approach can significantly reduce file size when only small portions of the screen
/// change between frames (e.g., typing, cursor movement).
/// </summary>
public class SvgEncoder(SessionOptions options, FrameStorage storage) : EncoderBase(options, storage)
{
    public override bool SupportsPath(string outputPath)
    {
        return Path.GetExtension(outputPath).Equals(".svg", StringComparison.OrdinalIgnoreCase);
    }

    public override async Task<string> RenderAsync(string outputPath, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        // Note: SVG is built from terminal-content snapshots, not PNG frame files, so we validate
        // snapshots below rather than calling the PNG-based ValidateFramesExist() — an SVG-only
        // recording deliberately captures no raster frames.
        progress?.Report("Loading terminal content snapshots...");

        // Get terminal content snapshots
        var allSnapshots = GetTerminalSnapshots();
        var frameMetadata = GetFrameMetadata();

        if (allSnapshots.Count == 0)
        {
            throw new InvalidOperationException("No terminal content snapshots found. SMIL SVG encoder requires terminal content capture during recording.");
        }

        // Filter snapshots based on trimming
        List<TerminalContentSnapshot> snapshots;
        if (Options.TrimmedFirstFrame.HasValue && Options.TrimmedLastFrame.HasValue)
        {
            snapshots = allSnapshots
                .Where(s => s.FrameNumber >= Options.TrimmedFirstFrame.Value &&
                           s.FrameNumber <= Options.TrimmedLastFrame.Value)
                .OrderBy(s => s.Timestamp)
                .ToList();
        }
        else
        {
            snapshots = allSnapshots
                .Where(s =>
                {
                    var meta = frameMetadata.FirstOrDefault(m => m.FrameNumber == s.FrameNumber);
                    return meta?.IsVisible ?? false;
                })
                .OrderBy(s => s.Timestamp)
                .ToList();
        }

        if (snapshots.Count == 0)
        {
            throw new InvalidOperationException("No snapshots found after filtering.");
        }

        // Capture the end of the recording BEFORE collapsing the trailing tail. The collapse
        // below drops frames identical to the final frame (e.g. the EndBuffer hold after a program
        // finishes) so the file stays small, but we still want the final frame to remain on screen
        // for that held duration. Using this timestamp for totalDuration stretches the final
        // visibility interval to the true end, so a looping SVG pauses on the final frame for the
        // captured hold (the EndBuffer window) instead of flashing it and instantly restarting.
        var lastFrameTimestamp = snapshots[^1].Timestamp;

        // Content-aware re-baseline: drop leading fully-blank frames (so a looping SVG starts
        // on content instead of flashing empty each cycle) and collapse a trailing static tail.
        var (keepStart, keepEnd) = ContentAnalysis.TrimBlankLoopRange(
            snapshots.Select(s => s.Content!).ToList());
        if (keepStart != 0 || keepEnd != snapshots.Count - 1)
        {
            snapshots = snapshots.GetRange(keepStart, keepEnd - keepStart + 1);
        }

        progress?.Report($"Processing {snapshots.Count} frames with SMIL row-diffing...");

        // Calculate baseline and duration. The duration runs to the last captured frame (the end of
        // any trailing static hold), not just the last content change, so the held final frame is
        // preserved as the closing visibility interval; the collapsed snapshot list above still
        // avoids emitting the duplicate trailing frames.
        var baselineTimestamp = snapshots[0].Timestamp;
        var totalDuration = (lastFrameTimestamp - baselineTimestamp).TotalSeconds / Options.PlaybackSpeed;

        // Convert to TerminalStateWithTime format
        var states = snapshots.Select(s => new TerminalStateWithTime
        {
            Content = s.Content!,
            TimestampSeconds = (s.Timestamp - baselineTimestamp).TotalSeconds / Options.PlaybackSpeed,
            IsCursorIdle = false // SMIL renderer handles cursor differently
        }).ToList();

        // Generate SVG with SMIL animations
        progress?.Report("Generating SVG...");
        var renderer = new SvgRenderer(Options);
        if (Options.FitToContent)
        {
            // Crop to the union of all frames' content so a row/column that appears partway
            // through the recording is never clipped.
            var extent = ContentExtent.Union(states.Select(s => s.Content));
            renderer.SetContentExtent(extent.Cols, extent.Rows);
        }
        await renderer.RenderAnimatedAsync(outputPath, states, totalDuration, cancellationToken);

        progress?.Report($"SMIL SVG exported to {outputPath}");

        return outputPath;
    }
}
