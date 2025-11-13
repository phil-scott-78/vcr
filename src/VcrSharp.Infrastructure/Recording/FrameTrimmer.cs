using VcrSharp.Core.Session;

namespace VcrSharp.Infrastructure.Recording;

/// <summary>
/// Handles trimming of captured frames based on activity timing.
/// Removes frames that fall outside the desired recording window
/// (FirstActivity - StartBuffer) to (LastActivity + EndBuffer).
/// </summary>
public class FrameTrimmer(SessionOptions options, SessionState state)
{
    private readonly int _framerate = options.Framerate;

    /// <summary>
    /// Calculates the frame range to keep based on activity frame numbers.
    /// Returns the range of frame numbers to keep.
    /// </summary>
    /// <returns>A tuple of (firstFrameToKeep, lastFrameToKeep), or null if no trimming needed.</returns>
    public (int firstFrame, int lastFrame)? CalculateFrameRange()
    {
        // If no activity was detected, keep all frames
        if (!state.FirstActivityFrameNumber.HasValue || !state.LastActivityFrameNumber.HasValue)
        {
            return null;
        }

        // Convert buffer durations to frame counts
        var startBufferFrames = (int)(options.StartBuffer.TotalSeconds * _framerate);
        var endBufferFrames = (int)(options.EndBuffer.TotalSeconds * _framerate);

        // Calculate frame range directly from activity frame numbers
        var firstFrame = state.FirstActivityFrameNumber.Value - startBufferFrames;
        var lastFrame = state.LastActivityFrameNumber.Value + endBufferFrames;

        // Ensure first frame is at least 1 (frames are 1-based)
        if (firstFrame < 1)
        {
            firstFrame = 1;
        }

        return (firstFrame, lastFrame);
    }

    /// <summary>
    /// Deletes frames outside the specified range.
    /// </summary>
    /// <param name="frameDirectory">Directory containing frame files.</param>
    /// <param name="firstFrame">First frame to keep (inclusive).</param>
    /// <param name="lastFrame">Last frame to keep (inclusive).</param>
    public void TrimFrames(string frameDirectory, int firstFrame, int lastFrame)
    {
        if (!Directory.Exists(frameDirectory))
            return;

        // Process both text and cursor frames
        var patterns = new[] { "frame-text-*.png", "frame-cursor-*.png" };

        foreach (var pattern in patterns)
        {
            var frameFiles = Directory.GetFiles(frameDirectory, pattern)
                .OrderBy(f => f)
                .ToList();

            foreach (var file in frameFiles)
            {
                var frameNumber = ExtractFrameNumber(file);
                if (frameNumber < firstFrame || frameNumber > lastFrame)
                {
                    File.Delete(file);
                }
            }
        }
    }

    /// <summary>
    /// Renames remaining frames to be sequential starting from 1.
    /// This ensures FFmpeg can process them correctly.
    /// </summary>
    /// <param name="frameDirectory">Directory containing frame files.</param>
    public void RenumberFrames(string frameDirectory)
    {
        if (!Directory.Exists(frameDirectory))
            return;

        // Process text frames
        var textFrames = Directory.GetFiles(frameDirectory, "frame-text-*.png")
            .OrderBy(f => ExtractFrameNumber(f))
            .ToList();

        var tempTextFiles = new List<string>();
        for (var i = 0; i < textFrames.Count; i++)
        {
            var tempName = Path.Combine(frameDirectory, $"temp-text-{i:D5}.png");
            File.Move(textFrames[i], tempName);
            tempTextFiles.Add(tempName);
        }

        for (var i = 0; i < tempTextFiles.Count; i++)
        {
            // Use 0-based indexing to match FFmpeg's %05d pattern expectation
            var finalName = Path.Combine(frameDirectory, $"frame-text-{i:D5}.png");
            File.Move(tempTextFiles[i], finalName);
        }

        // Process cursor frames
        var cursorFrames = Directory.GetFiles(frameDirectory, "frame-cursor-*.png")
            .OrderBy(f => ExtractFrameNumber(f))
            .ToList();

        var tempCursorFiles = new List<string>();
        for (var i = 0; i < cursorFrames.Count; i++)
        {
            var tempName = Path.Combine(frameDirectory, $"temp-cursor-{i:D5}.png");
            File.Move(cursorFrames[i], tempName);
            tempCursorFiles.Add(tempName);
        }

        for (var i = 0; i < tempCursorFiles.Count; i++)
        {
            // Use 0-based indexing to match FFmpeg's %05d pattern expectation
            var finalName = Path.Combine(frameDirectory, $"frame-cursor-{i:D5}.png");
            File.Move(tempCursorFiles[i], finalName);
        }
    }

    /// <summary>
    /// Extracts the frame number from a filename like "frame-text-00001.png" or "frame-cursor-00001.png".
    /// </summary>
    private int ExtractFrameNumber(string filename)
    {
        var name = Path.GetFileNameWithoutExtension(filename);
        var parts = name.Split('-');
        // Last part should be the frame number (e.g., "00001" from "frame-text-00001")
        if (parts.Length >= 3 && int.TryParse(parts[^1], out var number))
        {
            return number;
        }
        return 0;
    }
}