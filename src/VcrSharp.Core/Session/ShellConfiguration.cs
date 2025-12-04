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
    /// The flag used to execute a command string (e.g., "-c" for bash, "/c" for cmd).
    /// </summary>
    public string ExecutionFlag { get; }

    /// <summary>
    /// Command to return to an interactive shell after Exec commands complete.
    /// Includes prompt setup. Null if shell stays interactive automatically.
    /// </summary>
    public string? InteractiveReturnCommand { get; }

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
        Dictionary<string, string>? environment = null,
        string executionFlag = "-c",
        string? interactiveReturnCommand = null)
    {
        Name = name;
        DisplayName = displayName;
        DefaultPromptPattern = new Regex(promptPattern, RegexOptions.Multiline);
        _commandLineArgs = commandLineArgs ?? [];
        _initCommand = initCommand;
        Environment = environment ?? new Dictionary<string, string>();
        ExecutionFlag = executionFlag;
        InteractiveReturnCommand = interactiveReturnCommand;
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
    private static readonly Dictionary<string, ShellConfiguration> Registry = new(StringComparer.OrdinalIgnoreCase)
    {
        // PowerShell Core (pwsh)
        ["pwsh"] = new ShellConfiguration(
            name: "pwsh",
            displayName: "PowerShell",
            promptPattern: @">\s*$",  // PowerShell prompts end with "> " (with space)
            commandLineArgs: ["-Login", "-NoLogo", "-NoExit", "-NoProfile", "-Command"],
            initCommand: "Set-PSReadLineOption -HistorySaveStyle SaveNothing -PredictionSource None; " +
                        "function prompt { '> ' }",
            executionFlag: "-Command",
            // pwsh -NoExit keeps shell running after command execution
            interactiveReturnCommand: null  // Use -NoExit flag instead
        ),

        // Windows PowerShell (powershell)
        ["powershell"] = new ShellConfiguration(
            name: "powershell",
            displayName: "Windows PowerShell",
            promptPattern: @">\s*$",
            commandLineArgs: ["-NoLogo", "-NoExit", "-NoProfile", "-Command"],
            initCommand: "Set-PSReadLineOption -HistorySaveStyle SaveNothing -PredictionSource None; " +
                        "function prompt { '> ' }",
            executionFlag: "-Command",
            interactiveReturnCommand: null  // Use -NoExit flag instead
        ),

        // Bash
        ["bash"] = new ShellConfiguration(
            name: "bash",
            displayName: "Bash",
            promptPattern: @">\s*$",
            commandLineArgs: ["--noprofile", "--norc", "-c"],
            initCommand: "export PS1='> '; export BASH_SILENCE_DEPRECATION_WARNING=1; exec bash --noprofile --norc",
            environment: new Dictionary<string, string>(),
            executionFlag: "-c",
            interactiveReturnCommand: "export PS1='> '; exec bash --noprofile --norc"
        ),

        // Bourne Shell
        ["sh"] = new ShellConfiguration(
            name: "sh",
            displayName: "Bourne Shell",
            promptPattern: @">\s*$",
            commandLineArgs: ["-c"],
            initCommand: "PS1='> ' exec sh",
            executionFlag: "-c",
            interactiveReturnCommand: "PS1='> ' exec sh"
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
            },
            executionFlag: "-c",
            interactiveReturnCommand: "PROMPT='> ' exec zsh --histnostore --no-rcs"
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
            initCommand: null,
            executionFlag: "-c",
            interactiveReturnCommand: "exec fish --no-config --private -C 'function fish_greeting; end' -C 'function fish_prompt; echo -n \"> \"; end'"
        ),

        // CMD (Windows Command Prompt)
        // CMD uses /k to run command and remain interactive
        ["cmd"] = new ShellConfiguration(
            name: "cmd",
            displayName: "Command Prompt",
            promptPattern: @">\s*$",
            commandLineArgs: ["/k", "prompt=$G$S"],  // Set prompt to "> " ($G = >, $S = space)
            executionFlag: "/k",  // /k runs command and stays open
            interactiveReturnCommand: null  // /k keeps shell running
        ),

        ["cmd.exe"] = new ShellConfiguration(
            name: "cmd",  // Use "cmd" not "cmd.exe" as executable
            displayName: "Command Prompt",
            promptPattern: @">\s*$",
            commandLineArgs: ["/k", "prompt=$G$S"],
            executionFlag: "/k",
            interactiveReturnCommand: null
        ),
    };

    /// <summary>
    /// Default fallback configuration for unknown shells.
    /// Uses standardized ">" prompt pattern.
    /// Uses bash-style command-line arguments.
    /// </summary>
    private static readonly ShellConfiguration DefaultConfiguration = new ShellConfiguration(
        name: "bash",  // Default to bash
        displayName: "Default Shell",
        promptPattern: @">\s*$",
        commandLineArgs: ["-c"],
        initCommand: "PS1='> '",  // Set prompt to "> "
        executionFlag: "-c",
        interactiveReturnCommand: "PS1='> ' exec bash"
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
            return DefaultConfiguration;
        }

        // Try to find exact match
        if (Registry.TryGetValue(shellName, out var config))
        {
            return config;
        }

        // Try to extract shell name from path (e.g., "/bin/bash" -> "bash")
        var shellFileName = Path.GetFileNameWithoutExtension(shellName);
        if (!string.IsNullOrEmpty(shellFileName) && Registry.TryGetValue(shellFileName, out config))
        {
            return config;
        }

        // Return default configuration for unknown shells
        return DefaultConfiguration;
    }
}