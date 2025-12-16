using VcrSharp.Core.Session;
using VcrSharp.Infrastructure.Recording;
using VcrSharp.Infrastructure.Rendering.Encoders;

namespace VcrSharp.Infrastructure.Rendering;

/// <summary>
/// Orchestrates video rendering from captured frames.
/// Generates decoration assets, builds FFmpeg commands, and produces output videos.
/// </summary>
public class VideoEncoder
{
    private readonly SessionOptions _options;
    private readonly FrameStorage _storage;
    private readonly List<IEncoder> _encoders;

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

        // Register all available encoders
        // Note: Order matters - more specific patterns must come before general ones
        // - FramesEncoder handles directory paths (checked first)
        _encoders =
        [
            new FramesEncoder(options, storage),
            new GifEncoder(options, storage),
            new Mp4Encoder(options, storage),
            new WebMEncoder(options, storage),
            new PngEncoder(options, storage),
            new SvgEncoder(options, storage)
        ];
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

        // Render each output file
        // Note: Individual encoders will validate their own requirements
        // (e.g., FFmpeg-based encoders need manifests, SVG encoder needs terminal snapshots)
        var renderedFiles = new List<string>();
        foreach (var outputPath in _options.OutputFiles)
        {
            // Find encoder that supports this output path
            var encoder = _encoders.FirstOrDefault(e => e.SupportsPath(outputPath));
            if (encoder == null)
            {
                var extension = Path.GetExtension(outputPath);
                throw new NotSupportedException($"Output format '{extension}' is not supported. No encoder found for path: {outputPath}");
            }

            var formatName = GetFormatName(outputPath);
            progress?.Report($"Rendering {formatName}...");

            try
            {
                var renderedPath = await encoder.RenderAsync(outputPath, progress);
                renderedFiles.Add(renderedPath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to render {formatName} output: {ex.Message}", ex);
            }
        }

        return renderedFiles;
    }

    /// <summary>
    /// Gets a display name for the output format.
    /// </summary>
    private static string GetFormatName(string outputPath)
    {
        // Check if it's a directory (Frames encoder)
        if (Directory.Exists(outputPath) || !Path.HasExtension(outputPath))
        {
            return "FRAMES";
        }

        var extension = Path.GetExtension(outputPath).ToLowerInvariant().TrimStart('.');
        return extension.ToUpperInvariant();
    }

}