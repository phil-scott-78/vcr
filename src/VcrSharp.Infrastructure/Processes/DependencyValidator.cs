namespace VcrSharp.Infrastructure.Processes;

/// <summary>
/// Validates that required external dependencies are available.
/// </summary>
public static class DependencyValidator
{
    /// <summary>
    /// Validates that all required dependencies are available.
    /// </summary>
    /// <param name="requireTtyd">Whether ttyd is required.</param>
    /// <param name="requireFfmpeg">Whether ffmpeg is required.</param>
    /// <returns>A list of missing dependencies, or empty if all are available.</returns>
    public static List<string> ValidateDependencies(bool requireTtyd = true, bool requireFfmpeg = true)
    {
        var missing = new List<string>();

        if (requireTtyd && !ProcessHelper.IsProgramAvailable("ttyd"))
        {
            missing.Add("ttyd is not installed or not in PATH. Install from: https://github.com/tsl0922/ttyd");
        }

        if (requireFfmpeg && !ProcessHelper.IsProgramAvailable("ffmpeg"))
        {
            missing.Add("ffmpeg is not installed or not in PATH. Install from: https://ffmpeg.org/download.html");
        }

        return missing;
    }
}