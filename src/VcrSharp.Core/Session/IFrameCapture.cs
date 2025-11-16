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
}