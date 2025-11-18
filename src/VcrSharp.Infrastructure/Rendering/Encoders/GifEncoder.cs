using FFMpegCore;
using VcrSharp.Core.Session;
using VcrSharp.Infrastructure.Recording;

namespace VcrSharp.Infrastructure.Rendering.Encoders;

/// <summary>
/// Encoder that renders GIF output using FFMpegCore with layer compositing and palette generation.
/// Uses concat demuxer to support variable frame durations.
/// Matches VHS implementation with fps, setpts, and scale filters for optimal file size.
/// </summary>
public class GifEncoder(SessionOptions options, FrameStorage storage) : EncoderBase(options, storage)
{
    public override bool SupportsPath(string outputPath)
    {
        return Path.GetExtension(outputPath).Equals(".gif", StringComparison.OrdinalIgnoreCase);
    }

    public override async Task<string> RenderAsync(string outputPath, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        ValidateFramesExist();
        var (textManifest, cursorManifest) = GetManifestPaths();

        // Calculate terminal dimensions (content area without padding)
        var termWidth = Options.Width - 2 * Options.Padding;
        var termHeight = Options.Height - 2 * Options.Padding;

        // Log dimensions for debugging
        Core.Logging.VcrLogger.Logger.Debug(
            "GifEncoder dimensions - Width: {Width}, Height: {Height}, Padding: {Padding}, termWidth: {termWidth}, termHeight: {termHeight}",
            Options.Width, Options.Height, Options.Padding, termWidth, termHeight);

        var backgroundColor = Options.Theme.Background;

        // Build filter chain: handle padding=0 case differently to avoid scale+pad issues
        string filterComplex;
        if (Options.Padding == 0)
        {
            // No padding: simplified filter chain without scale/pad operations
            // This avoids issues with force_original_aspect_ratio and unnecessary padding
            filterComplex = $"[0:v][1:v]overlay=0:0[merged];" +
                           $"[merged]fps={Options.Framerate},setpts=PTS/{Options.PlaybackSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture)}[final];" +
                           $"[final]split[s0][s1];" +
                           $"[s0]palettegen=max_colors={Options.MaxColors}[p];" +
                           $"[s1][p]paletteuse";
        }
        else
        {
            // With padding: full filter chain with scale, pad, and fillborders
            filterComplex = $"[0:v][1:v]overlay=0:0[merged];" +
                           $"[merged]scale={termWidth}:{termHeight}:force_original_aspect_ratio=1[scaled];" +
                           $"[scaled]fps={Options.Framerate},setpts=PTS/{Options.PlaybackSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture)}[speed];" +
                           $"[speed]pad={Options.Width}:{Options.Height}:(ow-iw)/2:(oh-ih)/2:{backgroundColor}[padded];" +
                           $"[padded]fillborders=left={Options.Padding}:right={Options.Padding}:top={Options.Padding}:bottom={Options.Padding}:mode=fixed:color={backgroundColor}[final];" +
                           $"[final]split[s0][s1];" +
                           $"[s0]palettegen=max_colors={Options.MaxColors}[p];" +
                           $"[s1][p]paletteuse";
        }

        // Ensure output directory exists
        EnsureDirectoryExists(outputPath);

        await FFMpegArguments
            .FromFileInput(textManifest, verifyExists: true, options => options
                .WithCustomArgument("-f concat")
                .WithCustomArgument("-safe 0"))
            .AddFileInput(cursorManifest, verifyExists: true, options => options
                .WithCustomArgument("-f concat")
                .WithCustomArgument("-safe 0"))
            .OutputToFile(outputPath, overwrite: true, options => options
                .WithCustomArgument($"-filter_complex \"{filterComplex}\"")
                .ForceFormat("gif"))
            .NotifyOnOutput(_ =>
            {
                // Suppress verbose output - only critical errors will be shown
            })
            .NotifyOnError(_ =>
            {
                // Suppress verbose error output
            })
            .ProcessAsynchronously();

        return outputPath;
    }
}
