using FFMpegCore;
using VcrSharp.Core.Session;
using VcrSharp.Infrastructure.Recording;

namespace VcrSharp.Infrastructure.Rendering.Encoders;

/// <summary>
/// Encoder that renders PNG output (single frame) using FFMpegCore with layer compositing.
/// Uses concat demuxer to support variable frame durations.
/// </summary>
public class PngEncoder(SessionOptions options, FrameStorage storage) : EncoderBase(options, storage)
{
    public override bool SupportsPath(string outputPath)
    {
        return Path.GetExtension(outputPath).Equals(".png", StringComparison.OrdinalIgnoreCase);
    }

    public override async Task<string> RenderAsync(string outputPath, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        ValidateFramesExist();
        var (textManifest, cursorManifest) = GetManifestPaths();

        // Build filter chain with padding if needed
        var filterComplex = BuildFilterChain("[0:v][1:v]overlay=0:0");

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
                .WithCustomArgument("-frames:v 1"))
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

    /// <summary>
    /// Builds FFmpeg filter chain with optional padding.
    /// Matches VHS behavior: pad filter to expand canvas, fillborders to fill with background color.
    /// </summary>
    /// <param name="baseFilter">The base filter chain (e.g., overlay, palette, etc.)</param>
    /// <returns>Complete filter chain with padding applied if needed</returns>
    private string BuildFilterChain(string baseFilter)
    {
        if (Options.Padding <= 0)
        {
            // No padding - return base filter as-is
            return baseFilter;
        }

        // Extract background color from theme
        var backgroundColor = Options.Theme.Background;

        // Build filter chain with padding
        // 1. Apply base filter
        // 2. Pad to target dimensions (Width x Height) centered
        // 3. Fill borders with background color
        var filterChain = $"{baseFilter}[merged];" +
                          $"[merged]pad={Options.Width}:{Options.Height}:(ow-iw)/2:(oh-ih)/2:{backgroundColor}[padded];" +
                          $"[padded]fillborders=left={Options.Padding}:right={Options.Padding}:top={Options.Padding}:bottom={Options.Padding}:mode=fixed:color={backgroundColor}";

        return filterChain;
    }
}
