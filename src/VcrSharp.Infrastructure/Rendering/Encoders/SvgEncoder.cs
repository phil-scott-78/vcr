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
        ValidateFramesExist();

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

        progress?.Report($"Processing {snapshots.Count} frames with SMIL row-diffing...");

        // Calculate baseline and duration
        var baselineTimestamp = snapshots[0].Timestamp;
        var totalDuration = (snapshots[^1].Timestamp - baselineTimestamp).TotalSeconds / Options.PlaybackSpeed;

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
        await renderer.RenderAnimatedAsync(outputPath, states, totalDuration, cancellationToken);

        progress?.Report($"SMIL SVG exported to {outputPath}");

        return outputPath;
    }
}
