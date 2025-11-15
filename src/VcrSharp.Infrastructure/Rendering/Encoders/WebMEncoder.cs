using FFMpegCore;
using VcrSharp.Core.Session;
using VcrSharp.Infrastructure.Recording;

namespace VcrSharp.Infrastructure.Rendering.Encoders;

/// <summary>
/// Encoder that renders WebM output using FFMpegCore with VP9 encoding and layer compositing.
/// Uses concat demuxer to support variable frame durations.
/// Supports transparent backgrounds with yuva420p pixel format when TransparentBackground is enabled.
/// Uses optimized VP9 encoding settings for better quality.
/// </summary>
public class WebMEncoder(SessionOptions options, FrameStorage storage) : EncoderBase(options, storage)
{
    public override bool SupportsPath(string outputPath)
    {
        return Path.GetExtension(outputPath).Equals(".webm", StringComparison.OrdinalIgnoreCase);
    }

    public override async Task<string> RenderAsync(string outputPath, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        ValidateFramesExist();
        var (textManifest, cursorManifest) = GetManifestPaths();

        // Calculate terminal dimensions (content area without padding)
        var termWidth = Options.Width - 2 * Options.Padding;
        var termHeight = Options.Height - 2 * Options.Padding;

        var backgroundColor = Options.Theme.Background;
        var isTransparent = Options.TransparentBackground;

        // Build filter chain: handle padding=0 case differently to avoid scale+pad issues
        string filterComplex;
        if (Options.Padding == 0)
        {
            // No padding: simplified filter chain
            // Ensure even dimensions for VP9
            filterComplex = $"[0:v][1:v]overlay=0:0[merged];" +
                           $"[merged]fps={Options.Framerate},setpts=PTS/{Options.PlaybackSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture)}[speed];" +
                           $"[speed]scale='trunc(iw/2)*2':'trunc(ih/2)*2'";

            // Add format conversion for alpha channel preservation
            if (isTransparent)
            {
                filterComplex += "[scaled];[scaled]format=yuva420p";
            }
        }
        else
        {
            // With padding: full filter chain with scale, pad, and fillborders
            filterComplex = $"[0:v][1:v]overlay=0:0[merged];" +
                           $"[merged]scale={termWidth}:{termHeight}:force_original_aspect_ratio=1[scaled];" +
                           $"[scaled]fps={Options.Framerate},setpts=PTS/{Options.PlaybackSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture)}[speed];" +
                           $"[speed]scale='trunc(iw/2)*2':'trunc(ih/2)*2'[even];";

            if (isTransparent)
            {
                // Use transparent padding - pad filter with alpha channel
                filterComplex += $"[even]pad={Options.Width}:{Options.Height}:(ow-iw)/2:(oh-ih)/2:color=0x00000000[padded];" +
                               $"[padded]format=yuva420p";
            }
            else
            {
                // Use opaque background color for padding
                filterComplex += $"[even]pad={Options.Width}:{Options.Height}:(ow-iw)/2:(oh-ih)/2:{backgroundColor}[padded];" +
                               $"[padded]fillborders=left={Options.Padding}:right={Options.Padding}:top={Options.Padding}:bottom={Options.Padding}:mode=fixed:color={backgroundColor}";
            }
        }

        // Build custom arguments string with all VP9 encoding parameters
        var customArgs = $"-filter_complex \"{filterComplex}\" -b:v 0 -deadline good -cpu-used 1 -auto-alt-ref 1";

        // Add pixel format for alpha channel support
        if (isTransparent)
        {
            customArgs += " -pix_fmt yuva420p";
        }

        await FFMpegArguments
            .FromFileInput(textManifest, verifyExists: true, options => options
                .WithCustomArgument("-f concat")
                .WithCustomArgument("-safe 0"))
            .AddFileInput(cursorManifest, verifyExists: true, options => options
                .WithCustomArgument("-f concat")
                .WithCustomArgument("-safe 0"))
            .OutputToFile(outputPath, overwrite: true, options => options
                .WithVideoCodec("libvpx-vp9")
                .WithConstantRateFactor(30)  // Match VHS quality (was 31)
                .WithCustomArgument(customArgs))
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
