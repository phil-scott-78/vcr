using VcrSharp.Core.Session;

namespace VcrSharp.Infrastructure.Processes;

/// <summary>
/// Builds shell commands with proper arguments to keep shells interactive
/// and ensure consistent prompt behavior across different shell types.
/// </summary>
public static class ShellCommandBuilder
{
    /// <summary>
    /// Builds a shell command list suitable for ttyd that keeps the shell interactive
    /// with a consistent prompt and prevents immediate exit.
    /// </summary>
    /// <param name="shellName">The shell name (bash, zsh, pwsh, powershell, cmd, sh)</param>
    /// <returns>A list containing the shell executable and its arguments</returns>
    public static List<string> BuildShellCommand(string shellName)
    {
        var config = ShellConfiguration.GetConfiguration(shellName);
        return config.BuildTtydCommand();
    }
}