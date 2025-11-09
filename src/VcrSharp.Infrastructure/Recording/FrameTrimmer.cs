using VcrSharp.Core.Session;

namespace VcrSharp.Infrastructure.Recording;

/// <summary>
/// Handles trimming of captured frames based on activity timing.
/// Removes frames that fall outside the desired recording window
/// (FirstActivity - StartBuffer) to (LastActivity + EndBuffer).
/// </summary>
public class FrameTrimmer
{
    private readonly SessionOptions _options;
    private readonly SessionState _state;
    private readonly int _framerate;

    public FrameTrimmer(SessionOptions options, SessionState state)
    {
        _options = options;
        _state = state;
        _framerate = options.Framerate;
    }

    /// <summary>
    /// Calculates the frame range to keep based on activity timing.
    /// Returns the range of frame numbers to keep.
    /// </summary>
    /// <param name="actualStopTime">The actual time when frame capture stopped (optional ceiling).</param>
    /// <returns>A tuple of (firstFrameToKeep, lastFrameToKeep), or null if no trimming needed.</returns>
    public (int firstFrame, int lastFrame)? CalculateFrameRange(TimeSpan? actualStopTime = null)
    {
        // If no activity was detected, keep all frames
        if (!_state.FirstActivityTimestamp.HasValue)
        {
            return null;
        }

        // Calculate the recording window
        var recordingStart = _state.FirstActivityTimestamp.Value - _options.StartBuffer;
        var recordingEnd = _state.LastActivityTimestamp + _options.EndBuffer;

        // Ensure start is not negative
        if (recordingStart < TimeSpan.Zero)
        {
            recordingStart = TimeSpan.Zero;
        }

        // If actualStopTime is provided, use it as a ceiling for recordingEnd
        // This prevents keeping frames that were captured during the shutdown sequence
        if (actualStopTime.HasValue && recordingEnd > actualStopTime.Value)
        {
            recordingEnd = actualStopTime.Value;
        }

        // Convert timestamps to frame numbers (1-based, since frames start at 1)
        var firstFrame = TimeSpanToFrameNumber(recordingStart);
        var lastFrame = TimeSpanToFrameNumber(recordingEnd);

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
    /// Converts a TimeSpan to a frame number based on framerate.
    /// Frames are 1-based, so frame 1 is at time 0, frame 2 at 1/fps, etc.
    /// </summary>
    private int TimeSpanToFrameNumber(TimeSpan time)
    {
        // Calculate which frame index this timestamp corresponds to (0-based)
        // Then add 1 since frames are numbered starting at 1
        var frameIndex = (int)(time.TotalSeconds * _framerate);
        return frameIndex + 1;
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