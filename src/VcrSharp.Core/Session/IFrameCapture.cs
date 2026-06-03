namespace VcrSharp.Core.Session;

/// <summary>
/// Interface for frame capture operations.
/// Defines the contract for commands to capture screenshots without depending on Infrastructure types.
/// </summary>
public interface IFrameCapture
{
    /// <summary>
    /// Captures a screenshot and saves it to the specified file path.
    /// Format is auto-detected from file extension (.png for raster, .svg for vector).
    /// </summary>
    /// <param name="filePath">The file path where the screenshot should be saved.</param>
    Task CaptureScreenshotAsync(string filePath);

    /// <summary>
    /// Waits for the terminal buffer to stop changing (settle), or until the maximum wait elapses.
    /// Used by the Screenshot command to capture finished command output rather than a partial screen.
    /// </summary>
    /// <param name="inactivityTimeout">How long the buffer must be unchanged to be considered settled.</param>
    /// <param name="maxWait">Hard cap on total wait time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task WaitForBufferStableAsync(TimeSpan inactivityTimeout, TimeSpan maxWait, CancellationToken cancellationToken = default);
}