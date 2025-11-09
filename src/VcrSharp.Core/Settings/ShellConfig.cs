namespace VcrSharp.Core.Settings;

/// <summary>
/// Configuration for different shell environments.
/// </summary>
public class ShellConfig
{
    /// <summary>
    /// Gets or sets the shell executable name or path.
    /// </summary>
    public string Shell { get; set; } = "bash";

    /// <summary>
    /// Gets or sets the command-line arguments for the shell.
    /// </summary>
    public List<string> Args { get; set; } = [];

    /// <summary>
    /// Gets or sets the working directory for the shell.
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Gets or sets environment variables to set for the shell.
    /// </summary>
    public Dictionary<string, string> Environment { get; set; } = new();

    /// <summary>
    /// Gets a shell configuration for Bash.
    /// </summary>
    public static ShellConfig Bash => new()
    {
        Shell = "bash",
        Args = ["--noprofile", "--norc"]
    };

    /// <summary>
    /// Gets a shell configuration for Zsh.
    /// </summary>
    public static ShellConfig Zsh => new()
    {
        Shell = "zsh",
        Args = ["--no-rcs"]
    };

    /// <summary>
    /// Gets a shell configuration for Fish.
    /// </summary>
    public static ShellConfig Fish => new()
    {
        Shell = "fish",
        Args = ["--no-config"]
    };

    /// <summary>
    /// Gets a shell configuration for PowerShell.
    /// </summary>
    public static ShellConfig PowerShell => new()
    {
        Shell = "pwsh",
        Args = ["-NoProfile"]
    };

    /// <summary>
    /// Gets a shell configuration for Windows PowerShell.
    /// </summary>
    public static ShellConfig WindowsPowerShell => new()
    {
        Shell = "powershell",
        Args = ["-NoProfile"]
    };

    /// <summary>
    /// Gets a shell configuration for Command Prompt.
    /// </summary>
    public static ShellConfig Cmd => new()
    {
        Shell = "cmd",
        Args = ["/Q"]
    };

    /// <summary>
    /// Gets a shell configuration by name.
    /// </summary>
    /// <param name="shellName">The shell name (case-insensitive).</param>
    /// <returns>The shell configuration, or a default Bash configuration if not recognized.</returns>
    public static ShellConfig GetByName(string shellName)
    {
        return shellName.ToLowerInvariant() switch
        {
            "bash" => Bash,
            "zsh" => Zsh,
            "fish" => Fish,
            "pwsh" or "powershell-core" => PowerShell,
            "powershell" or "windows-powershell" => WindowsPowerShell,
            "cmd" or "command" => Cmd,
            _ => new ShellConfig { Shell = shellName }
        };
    }
}