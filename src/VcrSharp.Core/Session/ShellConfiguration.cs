using System.Text.RegularExpressions;

namespace VcrSharp.Core.Session;

/// <summary>
/// Shell-specific configuration settings including prompt patterns and command-line arguments.
/// </summary>
public class ShellConfiguration
{
    /// <summary>
    /// The name of the shell (e.g., "pwsh", "bash", "zsh").
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Default regex pattern to match shell prompt.
    /// Used by Wait command when no explicit pattern is provided.
    /// </summary>
    public Regex DefaultPromptPattern { get; }

    /// <summary>
    /// Friendly display name for the shell.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Command-line arguments to pass to the shell for ttyd.
    /// </summary>
    private readonly List<string> _commandLineArgs;

    /// <summary>
    /// Optional initialization command to run when shell starts.
    /// </summary>
    private readonly string? _initCommand;

    /// <summary>
    /// Environment variables to set for this shell.
    /// </summary>
    public Dictionary<string, string> Environment { get; }

    private ShellConfiguration(
        string name,
        string displayName,
        string promptPattern,
        List<string>? commandLineArgs = null,
        string? initCommand = null,
        Dictionary<string, string>? environment = null)
    {
        Name = name;
        DisplayName = displayName;
        DefaultPromptPattern = new Regex(promptPattern, RegexOptions.Multiline);
        _commandLineArgs = commandLineArgs ?? [];
        _initCommand = initCommand;
        Environment = environment ?? new Dictionary<string, string>();
    }

    /// <summary>
    /// Builds the complete command list for ttyd to execute this shell.
    /// </summary>
    /// <returns>List containing shell executable and all arguments.</returns>
    public List<string> BuildTtydCommand()
    {
        var command = new List<string> { Name };
        command.AddRange(_commandLineArgs);

        if (!string.IsNullOrEmpty(_initCommand))
        {
            command.Add(_initCommand);
        }

        return command;
    }

    /// <summary>
    /// Registry of known shell configurations.
    /// </summary>
    private static readonly Dictionary<string, ShellConfiguration> _registry = new(StringComparer.OrdinalIgnoreCase)
    {
        // PowerShell Core (pwsh)
        ["pwsh"] = new ShellConfiguration(
            name: "pwsh",
            displayName: "PowerShell",
            promptPattern: @">\s*$",  // PowerShell prompts end with "> " (with space)
            commandLineArgs: ["-Login", "-NoLogo", "-NoExit", "-NoProfile", "-Command"],
            initCommand: "Set-PSReadLineOption -HistorySaveStyle SaveNothing; " +
                        "function prompt { '> ' }"
        ),

        // Windows PowerShell (powershell)
        ["powershell"] = new ShellConfiguration(
            name: "powershell",
            displayName: "Windows PowerShell",
            promptPattern: @">\s*$",
            commandLineArgs: ["-NoLogo", "-NoExit", "-NoProfile", "-Command"],
            initCommand: "Set-PSReadLineOption -HistorySaveStyle SaveNothing; " +
                        "function prompt { '> ' }"
        ),

        // Bash
        ["bash"] = new ShellConfiguration(
            name: "bash",
            displayName: "Bash",
            promptPattern: @">\s*$",
            commandLineArgs: ["--noprofile", "--norc", "-c"],
            initCommand: "export PS1='> '; export BASH_SILENCE_DEPRECATION_WARNING=1; exec bash --noprofile --norc",
            environment: new Dictionary<string, string>()
        ),

        // Bourne Shell
        ["sh"] = new ShellConfiguration(
            name: "sh",
            displayName: "Bourne Shell",
            promptPattern: @">\s*$",
            commandLineArgs: ["-c"],
            initCommand: "PS1='> ' exec sh"
        ),

        // Zsh
        ["zsh"] = new ShellConfiguration(
            name: "zsh",
            displayName: "Z Shell",
            promptPattern: @">\s*$",
            commandLineArgs: ["--histnostore", "--no-rcs"],
            initCommand: null,
            environment: new Dictionary<string, string>
            {
                ["PROMPT"] = "> "
            }
        ),

        // Fish
        ["fish"] = new ShellConfiguration(
            name: "fish",
            displayName: "Fish",
            promptPattern: @">\s*$",
            commandLineArgs: [
                "--login",
                "--no-config",
                "--private",
                "-C", "function fish_greeting; end",
                "-C", "function fish_prompt; echo -n '> '; end"
            ],
            initCommand: null
        ),

        // CMD (Windows Command Prompt)
        ["cmd"] = new ShellConfiguration(
            name: "cmd",
            displayName: "Command Prompt",
            promptPattern: @">\s*$",
            commandLineArgs: ["/k", "prompt=$G$S"]  // Set prompt to "> " ($G = >, $S = space)
        ),

        ["cmd.exe"] = new ShellConfiguration(
            name: "cmd",  // Use "cmd" not "cmd.exe" as executable
            displayName: "Command Prompt",
            promptPattern: @">\s*$",
            commandLineArgs: ["/k", "prompt=$G$S"]
        ),
    };

    /// <summary>
    /// Default fallback configuration for unknown shells.
    /// Uses standardized ">" prompt pattern.
    /// Uses bash-style command-line arguments.
    /// </summary>
    private static readonly ShellConfiguration _defaultConfiguration = new ShellConfiguration(
        name: "bash",  // Default to bash
        displayName: "Default Shell",
        promptPattern: @">\s*$",
        commandLineArgs: ["-c"],
        initCommand: "PS1='> '"  // Set prompt to "> "
    );

    /// <summary>
    /// Gets the shell configuration for the specified shell name.
    /// Returns a default configuration if the shell is not recognized.
    /// </summary>
    /// <param name="shellName">The name of the shell (e.g., "pwsh", "bash").</param>
    /// <returns>The shell configuration.</returns>
    public static ShellConfiguration GetConfiguration(string? shellName)
    {
        if (string.IsNullOrWhiteSpace(shellName))
        {
            return _defaultConfiguration;
        }

        // Try to find exact match
        if (_registry.TryGetValue(shellName, out var config))
        {
            return config;
        }

        // Try to extract shell name from path (e.g., "/bin/bash" -> "bash")
        var shellFileName = Path.GetFileNameWithoutExtension(shellName);
        if (!string.IsNullOrEmpty(shellFileName) && _registry.TryGetValue(shellFileName, out config))
        {
            return config;
        }

        // Return default configuration for unknown shells
        return _defaultConfiguration;
    }
}