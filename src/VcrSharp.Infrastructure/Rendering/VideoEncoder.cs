using FFMpegCore;
using FFMpegCore.Enums;
using VcrSharp.Core.Session;
using VcrSharp.Infrastructure.Recording;

namespace VcrSharp.Infrastructure.Rendering;

/// <summary>
/// Orchestrates video rendering from captured frames.
/// Generates decoration assets, builds FFmpeg commands, and produces output videos.
/// </summary>
public class VideoEncoder
{
    private readonly SessionOptions _options;
    private readonly FrameStorage _storage;

    /// <summary>
    /// Initializes a new instance of VideoEncoder.
    /// </summary>
    /// <param name="options">Session options containing output and visual settings</param>
    /// <param name="storage">Frame storage containing captured frames</param>
    public VideoEncoder(SessionOptions options, FrameStorage storage)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(storage);
        _options = options;
        _storage = storage;
    }

    /// <summary>
    /// Renders all requested output formats.
    /// </summary>
    /// <param name="progress">Optional progress reporter for status updates</param>
    /// <returns>List of generated output file paths</returns>
    public async Task<List<string>> RenderAsync(IProgress<string>? progress = null)
    {
        if (_options.OutputFiles.Count == 0)
        {
            throw new InvalidOperationException("No output files specified. VideoEncoder.RenderAsync should only be called when output files are configured.");
        }

        var frameCount = _storage.CountFrames();
        if (frameCount == 0)
        {
            throw new InvalidOperationException("No frames captured to render");
        }

        // Use frames manifest files for concat demuxer (supports variable frame durations)
        var textManifest = _storage.GetFramesManifestPath("text");
        var cursorManifest = _storage.GetFramesManifestPath("cursor");

        // Verify manifest files exist
        if (!File.Exists(textManifest) || !File.Exists(cursorManifest))
        {
            throw new InvalidOperationException("Frame manifest files not found. Call GenerateFramesManifest() first.");
        }

        // Render each output file
        var renderedFiles = new List<string>();
        foreach (var outputFile in _options.OutputFiles)
        {
            var extension = Path.GetExtension(outputFile).ToLowerInvariant();
            var formatName = extension.TrimStart('.').ToUpperInvariant();

            progress?.Report($"Rendering {formatName}...");

            try
            {
                await (extension switch
                {
                    ".gif" => RenderGifAsync(textManifest, cursorManifest, outputFile),
                    ".mp4" => RenderMp4Async(textManifest, cursorManifest, outputFile),
                    ".webm" => RenderWebMAsync(textManifest, cursorManifest, outputFile),
                    ".png" => RenderPngAsync(textManifest, cursorManifest, outputFile),
                    _ => throw new NotSupportedException($"Output format '{extension}' is not supported")
                });

                renderedFiles.Add(outputFile);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to render {extension} output: {ex.Message}", ex);
            }
        }

        return renderedFiles;
    }

    /// <summary>
    /// Renders GIF output using FFMpegCore with layer compositing and palette generation.
    /// Uses concat demuxer to support variable frame durations.
    /// Matches VHS implementation with fps, setpts, and scale filters for optimal file size.
    /// </summary>
    private async Task RenderGifAsync(string textManifest, string cursorManifest, string outputFile)
    {
        // Calculate terminal dimensions (content area without padding)
        var termWidth = _options.Width - 2 * _options.Padding;
        var termHeight = _options.Height - 2 * _options.Padding;

        // Log dimensions for debugging
        Core.Logging.VcrLogger.Logger.Debug(
            "VideoEncoder dimensions - Width: {Width}, Height: {Height}, Padding: {Padding}, termWidth: {termWidth}, termHeight: {termHeight}",
            _options.Width, _options.Height, _options.Padding, termWidth, termHeight);

        var backgroundColor = _options.Theme.Background;

        // Build filter chain: handle padding=0 case differently to avoid scale+pad issues
        string filterComplex;
        if (_options.Padding == 0)
        {
            // No padding: simplified filter chain without scale/pad operations
            // This avoids issues with force_original_aspect_ratio and unnecessary padding
            filterComplex = $"[0:v][1:v]overlay=0:0[merged];" +
                           $"[merged]fps={_options.Framerate},setpts=PTS/{_options.PlaybackSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture)}[final];" +
                           $"[final]split[s0][s1];" +
                           $"[s0]palettegen=max_colors={_options.MaxColors}[p];" +
                           $"[s1][p]paletteuse";
        }
        else
        {
            // With padding: full filter chain with scale, pad, and fillborders
            filterComplex = $"[0:v][1:v]overlay=0:0[merged];" +
                           $"[merged]scale={termWidth}:{termHeight}:force_original_aspect_ratio=1[scaled];" +
                           $"[scaled]fps={_options.Framerate},setpts=PTS/{_options.PlaybackSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture)}[speed];" +
                           $"[speed]pad={_options.Width}:{_options.Height}:(ow-iw)/2:(oh-ih)/2:{backgroundColor}[padded];" +
                           $"[padded]fillborders=left={_options.Padding}:right={_options.Padding}:top={_options.Padding}:bottom={_options.Padding}:mode=fixed:color={backgroundColor}[final];" +
                           $"[final]split[s0][s1];" +
                           $"[s0]palettegen=max_colors={_options.MaxColors}[p];" +
                           $"[s1][p]paletteuse";
        }

        await FFMpegArguments
            .FromFileInput(textManifest, verifyExists: true, options => options
                .WithCustomArgument("-f concat")
                .WithCustomArgument("-safe 0"))
            .AddFileInput(cursorManifest, verifyExists: true, options => options
                .WithCustomArgument("-f concat")
                .WithCustomArgument("-safe 0"))
            .OutputToFile(outputFile, overwrite: true, options => options
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
    }

    /// <summary>
    /// Renders MP4 output using FFMpegCore with H.264 encoding and layer compositing.
    /// Uses concat demuxer to support variable frame durations.
    /// Matches VHS implementation with fps, setpts, and scale filters.
    /// </summary>
    private async Task RenderMp4Async(string textManifest, string cursorManifest, string outputFile)
    {
        // Calculate terminal dimensions (content area without padding)
        var termWidth = _options.Width - 2 * _options.Padding;
        var termHeight = _options.Height - 2 * _options.Padding;

        var backgroundColor = _options.Theme.Background;

        // Build filter chain: handle padding=0 case differently to avoid scale+pad issues
        string filterComplex;
        if (_options.Padding == 0)
        {
            // No padding: simplified filter chain
            // Ensure even dimensions for H.264 (yuv420p requires even width/height)
            filterComplex = $"[0:v][1:v]overlay=0:0[merged];" +
                           $"[merged]fps={_options.Framerate},setpts=PTS/{_options.PlaybackSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture)}[speed];" +
                           $"[speed]scale='trunc(iw/2)*2':'trunc(ih/2)*2'";
        }
        else
        {
            // With padding: full filter chain with scale, pad, and fillborders
            filterComplex = $"[0:v][1:v]overlay=0:0[merged];" +
                           $"[merged]scale={termWidth}:{termHeight}:force_original_aspect_ratio=1[scaled];" +
                           $"[scaled]fps={_options.Framerate},setpts=PTS/{_options.PlaybackSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture)}[speed];" +
                           $"[speed]scale='trunc(iw/2)*2':'trunc(ih/2)*2'[even];" +
                           $"[even]pad={_options.Width}:{_options.Height}:(ow-iw)/2:(oh-ih)/2:{backgroundColor}[padded];" +
                           $"[padded]fillborders=left={_options.Padding}:right={_options.Padding}:top={_options.Padding}:bottom={_options.Padding}:mode=fixed:color={backgroundColor}";
        }

        await FFMpegArguments
            .FromFileInput(textManifest, verifyExists: true, options => options
                .WithCustomArgument("-f concat")
                .WithCustomArgument("-safe 0"))
            .AddFileInput(cursorManifest, verifyExists: true, options => options
                .WithCustomArgument("-f concat")
                .WithCustomArgument("-safe 0"))
            .OutputToFile(outputFile, overwrite: true, options => options
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
    }

    /// <summary>
    /// Renders WebM output using FFMpegCore with VP9 encoding and layer compositing.
    /// Uses concat demuxer to support variable frame durations.
    /// Matches VHS implementation with fps, setpts, and scale filters.
    /// </summary>
    private async Task RenderWebMAsync(string textManifest, string cursorManifest, string outputFile)
    {
        // Calculate terminal dimensions (content area without padding)
        var termWidth = _options.Width - 2 * _options.Padding;
        var termHeight = _options.Height - 2 * _options.Padding;

        var backgroundColor = _options.Theme.Background;

        // Build filter chain: handle padding=0 case differently to avoid scale+pad issues
        string filterComplex;
        if (_options.Padding == 0)
        {
            // No padding: simplified filter chain
            // Ensure even dimensions for VP9
            filterComplex = $"[0:v][1:v]overlay=0:0[merged];" +
                           $"[merged]fps={_options.Framerate},setpts=PTS/{_options.PlaybackSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture)}[speed];" +
                           $"[speed]scale='trunc(iw/2)*2':'trunc(ih/2)*2'";
        }
        else
        {
            // With padding: full filter chain with scale, pad, and fillborders
            filterComplex = $"[0:v][1:v]overlay=0:0[merged];" +
                           $"[merged]scale={termWidth}:{termHeight}:force_original_aspect_ratio=1[scaled];" +
                           $"[scaled]fps={_options.Framerate},setpts=PTS/{_options.PlaybackSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture)}[speed];" +
                           $"[speed]scale='trunc(iw/2)*2':'trunc(ih/2)*2'[even];" +
                           $"[even]pad={_options.Width}:{_options.Height}:(ow-iw)/2:(oh-ih)/2:{backgroundColor}[padded];" +
                           $"[padded]fillborders=left={_options.Padding}:right={_options.Padding}:top={_options.Padding}:bottom={_options.Padding}:mode=fixed:color={backgroundColor}";
        }

        await FFMpegArguments
            .FromFileInput(textManifest, verifyExists: true, options => options
                .WithCustomArgument("-f concat")
                .WithCustomArgument("-safe 0"))
            .AddFileInput(cursorManifest, verifyExists: true, options => options
                .WithCustomArgument("-f concat")
                .WithCustomArgument("-safe 0"))
            .OutputToFile(outputFile, overwrite: true, options => options
                .WithVideoCodec("libvpx-vp9")
                .WithConstantRateFactor(30)  // Match VHS quality (was 31)
                .WithCustomArgument($"-filter_complex \"{filterComplex}\"")
                .WithCustomArgument("-b:v 0"))
            .NotifyOnOutput(_ =>
            {
                // Suppress verbose output
            })
            .NotifyOnError(_ =>
            {
                // Suppress verbose error output
            })
            .ProcessAsynchronously();
    }

    /// <summary>
    /// Renders PNG output (single frame) using FFMpegCore with layer compositing.
    /// Uses concat demuxer to support variable frame durations.
    /// </summary>
    private async Task RenderPngAsync(string textManifest, string cursorManifest, string outputFile)
    {
        // Build filter chain with padding if needed
        var filterComplex = BuildFilterChain("[0:v][1:v]overlay=0:0");

        await FFMpegArguments
            .FromFileInput(textManifest, verifyExists: true, options => options
                .WithCustomArgument("-f concat")
                .WithCustomArgument("-safe 0"))
            .AddFileInput(cursorManifest, verifyExists: true, options => options
                .WithCustomArgument("-f concat")
                .WithCustomArgument("-safe 0"))
            .OutputToFile(outputFile, overwrite: true, options => options
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
    }

    /// <summary>
    /// Builds FFmpeg filter chain with optional padding.
    /// Matches VHS behavior: pad filter to expand canvas, fillborders to fill with background color.
    /// </summary>
    /// <param name="baseFilter">The base filter chain (e.g., overlay, palette, etc.)</param>
    /// <returns>Complete filter chain with padding applied if needed</returns>
    private string BuildFilterChain(string baseFilter)
    {
        if (_options.Padding <= 0)
        {
            // No padding - return base filter as-is
            return baseFilter;
        }

        // Extract background color from theme
        var backgroundColor = _options.Theme.Background;

        // Build filter chain with padding
        // 1. Apply base filter
        // 2. Pad to target dimensions (Width x Height) centered
        // 3. Fill borders with background color
        var filterChain = $"{baseFilter}[merged];" +
                          $"[merged]pad={_options.Width}:{_options.Height}:(ow-iw)/2:(oh-ih)/2:{backgroundColor}[padded];" +
                          $"[padded]fillborders=left={_options.Padding}:right={_options.Padding}:top={_options.Padding}:bottom={_options.Padding}:mode=fixed:color={backgroundColor}";

        return filterChain;
    }
}