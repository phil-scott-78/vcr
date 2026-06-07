namespace VcrSharp.Infrastructure.Processes;

/// <summary>
/// Validates that required external dependencies are available. Since the native path needs no ttyd and
/// no browser, the only external dependency is FFmpeg — and only for video (GIF/MP4/WebM) outputs.
/// </summary>
public static class DependencyValidator
{
    /// <summary>
    /// Validates that all required dependencies are available.
    /// </summary>
    /// <param name="requireFfmpeg">Whether ffmpeg is required (only for GIF/MP4/WebM outputs).</param>
    /// <returns>A list of missing dependencies, or empty if all are available.</returns>
    public static List<string> ValidateDependencies(bool requireFfmpeg = true)
    {
        var missing = new List<string>();

        if (requireFfmpeg && !ProcessHelper.IsProgramAvailable("ffmpeg"))
        {
            missing.Add("ffmpeg is not installed or not in PATH. Install from: https://ffmpeg.org/download.html");
        }

        return missing;
    }
}
