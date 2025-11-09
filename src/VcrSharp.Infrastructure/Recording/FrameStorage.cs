using System.Text;
using VcrSharp.Core.Recording;

namespace VcrSharp.Infrastructure.Recording;

/// <summary>
/// Manages temporary frame storage for terminal recording sessions.
/// Creates temp directories, generates sequential frame filenames, and handles cleanup.
/// </summary>
public class FrameStorage : IDisposable
{
    private bool _disposed;
    private readonly List<FrameMetadata> _frameMetadata = [];
    private readonly Lock _metadataLock = new();

    /// <summary>
    /// Gets the temporary directory where frames are stored.
    /// </summary>
    public string FrameDirectory { get; }

    /// <summary>
    /// Initializes a new instance of FrameStorage with a unique temp directory.
    /// </summary>
    public FrameStorage()
    {
        // Create unique temp directory for this recording session
        var tempPath = Path.GetTempPath();
        var sessionId = Path.GetRandomFileName();
        FrameDirectory = Path.Combine(tempPath, $"vcrsharp-{sessionId}");

        Directory.CreateDirectory(FrameDirectory);
    }

    /// <summary>
    /// Initializes a new instance of FrameStorage with a specified directory.
    /// </summary>
    /// <param name="directory">The directory to use for frame storage</param>
    public FrameStorage(string directory)
    {
        FrameDirectory = directory;
        Directory.CreateDirectory(FrameDirectory);
    }

    /// <summary>
    /// Gets the file path for a specific frame layer (text or cursor).
    /// Uses VHS-style naming: frame-text-00001.png, frame-cursor-00001.png
    /// </summary>
    /// <param name="frameNumber">The frame number (1-based)</param>
    /// <param name="layer">Layer name ("text" or "cursor")</param>
    /// <returns>Full path to the frame layer file</returns>
    public string GetFrameLayerPath(int frameNumber, string layer)
    {
        if (frameNumber < 0)
            throw new ArgumentException("Frame number must be non-negative", nameof(frameNumber));

        if (string.IsNullOrWhiteSpace(layer))
            throw new ArgumentException("Layer name cannot be empty", nameof(layer));

        var filename = $"frame-{layer}-{frameNumber:D5}.png";
        return Path.Combine(FrameDirectory, filename);
    }

    /// <summary>
    /// Counts the number of frames currently stored.
    /// </summary>
    /// <returns>The number of PNG files matching the frame pattern</returns>
    public int CountFrames()
    {
        if (!Directory.Exists(FrameDirectory))
            return 0;

        var files = Directory.GetFiles(FrameDirectory, "frame-*.png");
        return files.Length;
    }

    /// <summary>
    /// Records metadata for a captured frame.
    /// </summary>
    /// <param name="metadata">The frame metadata to record</param>
    public void RecordFrameMetadata(FrameMetadata metadata)
    {
        lock (_metadataLock)
        {
            _frameMetadata.Add(metadata);
        }
    }

    /// <summary>
    /// Calculates frame durations based on captured timestamps and writes the frames.txt manifest.
    /// Each frame's duration is the time until the next frame (last frame uses default interval).
    /// </summary>
    /// <param name="defaultFrameInterval">Default interval to use for the last frame</param>
    public void GenerateFramesManifest(TimeSpan defaultFrameInterval)
    {
        lock (_metadataLock)
        {
            if (_frameMetadata.Count == 0)
                return;

            // Calculate durations based on timestamps
            for (var i = 0; i < _frameMetadata.Count; i++)
            {
                if (i < _frameMetadata.Count - 1)
                {
                    // Duration is time until next frame
                    _frameMetadata[i].Duration = _frameMetadata[i + 1].Timestamp - _frameMetadata[i].Timestamp;
                }
                else
                {
                    // Last frame uses default interval
                    _frameMetadata[i].Duration = defaultFrameInterval;
                }
            }

            // Write text layer frames.txt
            WriteFramesManifest("text");

            // Write cursor layer frames.txt
            WriteFramesManifest("cursor");
        }
    }

    /// <summary>
    /// Writes the frames.txt manifest file for FFmpeg concat demuxer.
    /// </summary>
    /// <param name="layer">Layer name ("text" or "cursor")</param>
    private void WriteFramesManifest(string layer)
    {
        var manifestPath = Path.Combine(FrameDirectory, $"frames-{layer}.txt");
        var sb = new StringBuilder();

        foreach (var frame in _frameMetadata)
        {
            if (!frame.IsVisible)
                continue;

            var filename = $"frame-{layer}-{frame.FrameNumber:D5}.png";
            sb.AppendLine($"file '{filename}'");
            sb.AppendLine($"duration {frame.Duration.TotalSeconds:F6}");
        }

        // FFmpeg concat demuxer requires the last file to be listed again without duration
        if (_frameMetadata.Count > 0)
        {
            var lastFrame = _frameMetadata[^1];
            var filename = $"frame-{layer}-{lastFrame.FrameNumber:D5}.png";
            sb.AppendLine($"file '{filename}'");
        }

        File.WriteAllText(manifestPath, sb.ToString());
    }

    /// <summary>
    /// Gets the path to the frames manifest file for a specific layer.
    /// </summary>
    /// <param name="layer">Layer name ("text" or "cursor")</param>
    /// <returns>Full path to the frames manifest file</returns>
    public string GetFramesManifestPath(string layer)
    {
        return Path.Combine(FrameDirectory, $"frames-{layer}.txt");
    }

    /// <summary>
    /// Deletes all frames and the temporary directory.
    /// </summary>
    public void Cleanup()
    {
        if (_disposed || !Directory.Exists(FrameDirectory))
            return;

        try
        {
            Directory.Delete(FrameDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Directory might be in use, ignore
        }
        catch (UnauthorizedAccessException)
        {
            // Insufficient permissions, ignore
        }
    }

    /// <summary>
    /// Disposes resources and cleans up temp directory.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        Cleanup();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizer to ensure cleanup.
    /// </summary>
    ~FrameStorage()
    {
        Cleanup();
    }
}