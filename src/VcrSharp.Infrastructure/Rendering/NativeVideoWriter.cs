using System.Globalization;
using System.Text;
using FFMpegCore;
using SixLabors.ImageSharp;
using VcrSharp.Core.Rendering;
using VcrSharp.Core.Session;

namespace VcrSharp.Infrastructure.Rendering;

/// <summary>
/// Produces GIF / MP4 / WebM / PNG from the native capture by rasterizing each grid frame
/// (<see cref="RasterRenderer"/>) to PNG and feeding the sequence to FFmpeg via a concat manifest with
/// per-frame durations — the same FFmpeg pipeline the browser path uses, minus ttyd/Chromium.
/// </summary>
public static class NativeVideoWriter
{
    /// <summary>Renders the captured states to <paramref name="outputPath"/> (format by extension).
    /// Returns the number of frames emitted.</summary>
    public static async Task<int> WriteAsync(IReadOnlyList<TerminalStateWithTime> raw, double totalSeconds,
        SessionOptions options, string outputPath, CancellationToken cancellationToken = default)
    {
        if (raw.Count == 0) return 0;

        // Trim leading-blank / trailing-static frames and rebaseline, like the SVG writer.
        var (keepStart, keepEnd) = ContentAnalysis.TrimBlankLoopRange(raw.Select(s => s.Content).ToList());
        var kept = raw.Skip(keepStart).Take(keepEnd - keepStart + 1).ToList();
        if (kept.Count == 0) return 0;
        var baseline = kept[0].TimestampSeconds;
        var total = Math.Max(kept[^1].TimestampSeconds - baseline, totalSeconds - baseline);

        EnsureDirectoryExists(outputPath);
        var ext = Path.GetExtension(outputPath).ToLowerInvariant();
        var renderer = new RasterRenderer(options);

        // PNG = a single still of the final frame (no FFmpeg).
        if (ext == ".png")
        {
            using var img = renderer.Render(kept[^1].Content);
            await img.SaveAsPngAsync(outputPath, cancellationToken);
            return 1;
        }

        var temp = Directory.CreateTempSubdirectory("vcr-native-frames");
        try
        {
            var manifest = new StringBuilder();
            string? lastFramePath = null;
            for (var i = 0; i < kept.Count; i++)
            {
                var framePath = Path.Combine(temp.FullName, $"f{i:D5}.png");
                using (var img = renderer.Render(kept[i].Content))
                    await img.SaveAsPngAsync(framePath, cancellationToken);
                lastFramePath = framePath;

                var ts = kept[i].TimestampSeconds - baseline;
                var next = i < kept.Count - 1 ? kept[i + 1].TimestampSeconds - baseline : total;
                var duration = Math.Max(0.04, next - ts);
                manifest.Append("file '").Append(framePath.Replace('\\', '/')).Append("'\n");
                manifest.Append("duration ").Append(duration.ToString("0.###", CultureInfo.InvariantCulture)).Append('\n');
            }
            manifest.Append("file '").Append(lastFramePath!.Replace('\\', '/')).Append("'\n"); // concat needs the last file repeated

            var manifestPath = Path.Combine(temp.FullName, "frames.txt");
            await File.WriteAllTextAsync(manifestPath, manifest.ToString(), cancellationToken);

            // PlaybackSpeed feeds setpts=PTS/{speed}; 0 or negative would be a divide-by-zero in the filtergraph.
            var speed = (options.PlaybackSpeed > 0 ? options.PlaybackSpeed : 1.0).ToString(CultureInfo.InvariantCulture);
            var fps = options.Framerate;
            var bg = options.Theme.Background;

            await FFMpegArguments
                .FromFileInput(manifestPath, verifyExists: false, o => o
                    .WithCustomArgument("-f concat")
                    .WithCustomArgument("-safe 0"))
                .OutputToFile(outputPath, overwrite: true, o => ConfigureOutput(o, ext, fps, speed, options.MaxColors, options.ResolveGifLoopArgument(), bg))
                .NotifyOnError(_ => { })
                .NotifyOnOutput(_ => { })
                .ProcessAsynchronously();

            return kept.Count;
        }
        finally
        {
            try { temp.Delete(recursive: true); } catch { /* ignore */ }
        }
    }

    /// <summary>
    /// Writes the captured states to <paramref name="outputDir"/> as a numbered PNG sequence plus a
    /// <c>frames.txt</c> FFmpeg concat manifest (per-frame durations) — the browserless equivalent of the
    /// old FramesEncoder, but single-layer (composited) since the native raster path has no separate
    /// text/cursor layers. Returns the number of frames written.
    /// </summary>
    public static async Task<int> WriteFramesDirectoryAsync(IReadOnlyList<TerminalStateWithTime> raw, double totalSeconds,
        SessionOptions options, string outputDir, CancellationToken cancellationToken = default)
    {
        if (raw.Count == 0) return 0;

        var (keepStart, keepEnd) = ContentAnalysis.TrimBlankLoopRange(raw.Select(s => s.Content).ToList());
        var kept = raw.Skip(keepStart).Take(keepEnd - keepStart + 1).ToList();
        if (kept.Count == 0) return 0;
        var baseline = kept[0].TimestampSeconds;
        var total = Math.Max(kept[^1].TimestampSeconds - baseline, totalSeconds - baseline);

        Directory.CreateDirectory(outputDir);
        var renderer = new RasterRenderer(options);
        var manifest = new StringBuilder();
        string? lastName = null;

        for (var i = 0; i < kept.Count; i++)
        {
            var name = $"frame-{i:D5}.png";
            using (var img = renderer.Render(kept[i].Content))
                await img.SaveAsPngAsync(Path.Combine(outputDir, name), cancellationToken);
            lastName = name;

            var ts = kept[i].TimestampSeconds - baseline;
            var next = i < kept.Count - 1 ? kept[i + 1].TimestampSeconds - baseline : total;
            var duration = Math.Max(0.04, next - ts);
            manifest.Append("file '").Append(name).Append("'\n");
            manifest.Append("duration ").Append(duration.ToString("0.###", CultureInfo.InvariantCulture)).Append('\n');
        }
        manifest.Append("file '").Append(lastName!).Append("'\n"); // concat needs the last file repeated

        await File.WriteAllTextAsync(Path.Combine(outputDir, "frames.txt"), manifest.ToString(), cancellationToken);
        return kept.Count;
    }

    private static void ConfigureOutput(FFMpegArgumentOptions o, string ext, int fps, string speed, int maxColors, int loop, string bg)
    {
        switch (ext)
        {
            case ".gif":
                o.WithCustomArgument(
                        $"-filter_complex \"[0:v]fps={fps},setpts=PTS/{speed}[s];" +
                        // palettegen rejects max_colors below 4 — clamp so `Set MaxColors 2` still encodes.
                        $"[s]split[a][b];[a]palettegen=max_colors={Math.Max(4, maxColors)}[p];[b][p]paletteuse\"")
                    .WithCustomArgument($"-loop {loop}")
                    .ForceFormat("gif");
                break;
            case ".webm":
                o.WithCustomArgument(
                        $"-vf \"fps={fps},setpts=PTS/{speed},pad=ceil(iw/2)*2:ceil(ih/2)*2:0:0:color={bg}\"")
                    .WithCustomArgument("-c:v libvpx-vp9")
                    .WithCustomArgument("-pix_fmt yuva420p")
                    .ForceFormat("webm");
                break;
            default: // .mp4
                o.WithCustomArgument(
                        $"-vf \"fps={fps},setpts=PTS/{speed},pad=ceil(iw/2)*2:ceil(ih/2)*2:0:0:color={bg}\"")
                    .WithCustomArgument("-c:v libx264")
                    .WithCustomArgument("-pix_fmt yuv420p")
                    .ForceFormat("mp4");
                break;
        }
    }

    private static void EnsureDirectoryExists(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
    }
}
