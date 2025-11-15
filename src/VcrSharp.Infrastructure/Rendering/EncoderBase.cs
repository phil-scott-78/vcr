using VcrSharp.Core.Recording;
using VcrSharp.Core.Session;
using VcrSharp.Infrastructure.Recording;

namespace VcrSharp.Infrastructure.Rendering;

/// <summary>
/// Base class for output encoders, providing common functionality and access to session data.
/// </summary>
public abstract class EncoderBase : IEncoder
{
    /// <summary>
    /// Gets the session options containing output and visual settings.
    /// </summary>
    protected SessionOptions Options { get; }

    /// <summary>
    /// Gets the frame storage containing captured frames and metadata.
    /// </summary>
    protected FrameStorage Storage { get; }

    /// <summary>
    /// Initializes a new instance of the EncoderBase class.
    /// </summary>
    /// <param name="options">Session options containing output and visual settings.</param>
    /// <param name="storage">Frame storage containing captured frames.</param>
    protected EncoderBase(SessionOptions options, FrameStorage storage)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(storage);

        Options = options;
        Storage = storage;
    }

    /// <summary>
    /// Renders the captured frames to the specified output path.
    /// </summary>
    /// <param name="outputPath">The path where the output should be written (file or directory).</param>
    /// <param name="progress">Optional progress reporter for status updates.</param>
    /// <param name="cancellationToken">Cancellation token to stop the rendering process.</param>
    /// <returns>The actual output path (may differ from input for normalization).</returns>
    public abstract Task<string> RenderAsync(string outputPath, IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines if this encoder supports the given output path.
    /// </summary>
    /// <param name="outputPath">The output path to check.</param>
    /// <returns>True if this encoder can handle the output path.</returns>
    public abstract bool SupportsPath(string outputPath);

    /// <summary>
    /// Gets the frame metadata list from storage using reflection (since it's private).
    /// This is a helper method for encoders that need access to frame timing information.
    /// </summary>
    /// <returns>Read-only list of frame metadata.</returns>
    protected IReadOnlyList<FrameMetadata> GetFrameMetadata()
    {
        var field = typeof(FrameStorage).GetField("_frameMetadata",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (field?.GetValue(Storage) is List<FrameMetadata> metadata)
        {
            return metadata.AsReadOnly();
        }

        return Array.Empty<FrameMetadata>();
    }

    /// <summary>
    /// Gets all terminal content snapshots from storage.
    /// This is a helper method for encoders that need terminal text/styling information (e.g., SVG).
    /// </summary>
    /// <returns>Read-only list of terminal content snapshots.</returns>
    protected IReadOnlyList<TerminalContentSnapshot> GetTerminalSnapshots()
    {
        return Storage.GetTerminalSnapshots();
    }

    /// <summary>
    /// Validates that frames have been captured and manifests exist.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no frames are available.</exception>
    protected void ValidateFramesExist()
    {
        var frameCount = Storage.CountFrames();
        if (frameCount == 0)
        {
            throw new InvalidOperationException("No frames captured to render");
        }
    }

    /// <summary>
    /// Gets the paths to the frame manifest files.
    /// </summary>
    /// <returns>Tuple containing text and cursor manifest paths.</returns>
    protected (string textManifest, string cursorManifest) GetManifestPaths()
    {
        var textManifest = Storage.GetFramesManifestPath("text");
        var cursorManifest = Storage.GetFramesManifestPath("cursor");

        // Verify manifest files exist
        if (!File.Exists(textManifest) || !File.Exists(cursorManifest))
        {
            throw new InvalidOperationException("Frame manifest files not found. Call GenerateFramesManifest() first.");
        }

        return (textManifest, cursorManifest);
    }
}
