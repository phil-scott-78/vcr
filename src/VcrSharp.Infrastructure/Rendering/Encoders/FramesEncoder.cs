using System.Text;
using VcrSharp.Core.Session;
using VcrSharp.Infrastructure.Recording;

namespace VcrSharp.Infrastructure.Rendering.Encoders;

/// <summary>
/// Encoder that outputs raw PNG frames and manifest files to a directory.
/// Preserves the two-layer structure (text + cursor) and generates FFmpeg concat format manifests.
/// </summary>
public class FramesEncoder : EncoderBase
{
    public FramesEncoder(SessionOptions options, FrameStorage storage)
        : base(options, storage)
    {
    }

    public override bool SupportsPath(string outputPath)
    {
        // Supports directory paths (no extension) or paths that look like directories
        return !Path.HasExtension(outputPath) || Directory.Exists(outputPath);
    }

    public override async Task<string> RenderAsync(string outputPath, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        ValidateFramesExist();

        // Ensure output directory exists
        var outputDir = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(outputDir);

        progress?.Report("Copying frame files...");

        // Get frame metadata
        var frameMetadata = GetFrameMetadata();

        // Copy all frame files (both text and cursor layers)
        foreach (var metadata in frameMetadata)
        {
            if (!metadata.IsVisible)
                continue;

            cancellationToken.ThrowIfCancellationRequested();

            // Copy text layer
            var sourceTextPath = Storage.GetFrameLayerPath(metadata.FrameNumber, "text");
            var destTextPath = Path.Combine(outputDir, Path.GetFileName(sourceTextPath));
            if (File.Exists(sourceTextPath))
            {
                File.Copy(sourceTextPath, destTextPath, overwrite: true);
            }

            // Copy cursor layer
            var sourceCursorPath = Storage.GetFrameLayerPath(metadata.FrameNumber, "cursor");
            var destCursorPath = Path.Combine(outputDir, Path.GetFileName(sourceCursorPath));
            if (File.Exists(sourceCursorPath))
            {
                File.Copy(sourceCursorPath, destCursorPath, overwrite: true);
            }
        }

        progress?.Report("Generating manifest files...");

        // Generate manifest files
        await Task.Run(() =>
        {
            WriteFramesManifest(outputDir, "text", frameMetadata);
            WriteFramesManifest(outputDir, "cursor", frameMetadata);
        }, cancellationToken);

        progress?.Report($"Frames exported to {outputDir}");

        return outputDir;
    }

    /// <summary>
    /// Writes the frames.txt manifest file for FFmpeg concat demuxer.
    /// </summary>
    /// <param name="outputDir">Output directory path.</param>
    /// <param name="layer">Layer name ("text" or "cursor").</param>
    /// <param name="frameMetadata">Frame metadata list.</param>
    private static void WriteFramesManifest(string outputDir, string layer, IReadOnlyList<Core.Recording.FrameMetadata> frameMetadata)
    {
        var manifestPath = Path.Combine(outputDir, $"frames-{layer}.txt");
        var sb = new StringBuilder();

        foreach (var frame in frameMetadata)
        {
            if (!frame.IsVisible)
                continue;

            var filename = $"frame-{layer}-{frame.FrameNumber:D5}.png";
            sb.AppendLine($"file '{filename}'");
            sb.AppendLine($"duration {frame.Duration.TotalSeconds:F6}");
        }

        // FFmpeg concat demuxer requires the last file to be listed again without duration
        if (frameMetadata.Count > 0)
        {
            var lastFrame = frameMetadata[^1];
            var filename = $"frame-{layer}-{lastFrame.FrameNumber:D5}.png";
            sb.AppendLine($"file '{filename}'");
        }

        File.WriteAllText(manifestPath, sb.ToString());
    }
}
