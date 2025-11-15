using FFMpegCore;
using FFMpegCore.Enums;
using VcrSharp.Core.Session;
using VcrSharp.Infrastructure.Recording;

namespace VcrSharp.Infrastructure.Rendering.Encoders;

/// <summary>
/// Encoder that renders MP4 output using FFMpegCore with H.264 encoding and layer compositing.
/// Uses concat demuxer to support variable frame durations.
/// Matches VHS implementation with fps, setpts, and scale filters.
/// </summary>
public class Mp4Encoder(SessionOptions options, FrameStorage storage) : EncoderBase(options, storage)
{
    public override bool SupportsPath(string outputPath)
    {
        return Path.GetExtension(outputPath).Equals(".mp4", StringComparison.OrdinalIgnoreCase);
    }

    public override async Task<string> RenderAsync(string outputPath, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        ValidateFramesExist();
        var (textManifest, cursorManifest) = GetManifestPaths();

        // Calculate terminal dimensions (content area without padding)
        var termWidth = Options.Width - 2 * Options.Padding;
        var termHeight = Options.Height - 2 * Options.Padding;

        var backgroundColor = Options.Theme.Background;

        // Build filter chain: handle padding=0 case differently to avoid scale+pad issues
        string filterComplex;
        if (Options.Padding == 0)
        {
            // No padding: simplified filter chain
            // Ensure even dimensions for H.264 (yuv420p requires even width/height)
            filterComplex = $"[0:v][1:v]overlay=0:0[merged];" +
                           $"[merged]fps={Options.Framerate},setpts=PTS/{Options.PlaybackSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture)}[speed];" +
                           $"[speed]scale='trunc(iw/2)*2':'trunc(ih/2)*2'";
        }
        else
        {
            // With padding: full filter chain with scale, pad, and fillborders
            filterComplex = $"[0:v][1:v]overlay=0:0[merged];" +
                           $"[merged]scale={termWidth}:{termHeight}:force_original_aspect_ratio=1[scaled];" +
                           $"[scaled]fps={Options.Framerate},setpts=PTS/{Options.PlaybackSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture)}[speed];" +
                           $"[speed]scale='trunc(iw/2)*2':'trunc(ih/2)*2'[even];" +
                           $"[even]pad={Options.Width}:{Options.Height}:(ow-iw)/2:(oh-ih)/2:{backgroundColor}[padded];" +
                           $"[padded]fillborders=left={Options.Padding}:right={Options.Padding}:top={Options.Padding}:bottom={Options.Padding}:mode=fixed:color={backgroundColor}";
        }

        await FFMpegArguments
            .FromFileInput(textManifest, verifyExists: true, options => options
                .WithCustomArgument("-f concat")
                .WithCustomArgument("-safe 0"))
            .AddFileInput(cursorManifest, verifyExists: true, options => options
                .WithCustomArgument("-f concat")
                .WithCustomArgument("-safe 0"))
            .OutputToFile(outputPath, overwrite: true, options => options
                .WithVideoCodec(VideoCodec.LibX264)
                .WithConstantRateFactor(20)  // Match VHS quality (was 23)
                .WithCustomArgument($"-filter_complex \"{filterComplex}\"")
                .WithCustomArgument("-pix_fmt yuv420p")
                .WithCustomArgument("-movflags +faststart"))
            .NotifyOnOutput(_ =>
            {
                // Suppress verbose output
            })
            .NotifyOnError(_ =>
            {
                // Suppress verbose error output
            })
            .ProcessAsynchronously();

        return outputPath;
    }
}
