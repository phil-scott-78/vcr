namespace VcrSharp.Infrastructure.Rendering;

/// <summary>
/// Defines the contract for output encoders that convert captured frames
/// to various output formats (GIF, MP4, WebM, SVG, raw frames, etc.).
/// </summary>
public interface IEncoder
{
    /// <summary>
    /// Renders the captured frames to the specified output path.
    /// </summary>
    /// <param name="outputPath">The path where the output should be written (file or directory).</param>
    /// <param name="progress">Optional progress reporter for status updates.</param>
    /// <param name="cancellationToken">Cancellation token to stop the rendering process.</param>
    /// <returns>The actual output path (may differ from input for normalization).</returns>
    Task<string> RenderAsync(string outputPath, IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines if this encoder supports the given output path.
    /// Typically based on file extension or path characteristics (e.g., directory vs file).
    /// </summary>
    /// <param name="outputPath">The output path to check.</param>
    /// <returns>True if this encoder can handle the output path.</returns>
    bool SupportsPath(string outputPath);
}
