using VcrSharp.Cli.Commands;
using VcrSharp.Core.Parsing.Ast;

namespace VcrSharp.Cli.Helpers;

/// <summary>
/// Builds ICommand lists programmatically from CLI settings.
/// Allows direct capture commands to reuse VcrSession without tape files.
/// </summary>
public static class CommandListBuilder
{
    /// <summary>
    /// Builds a command list for a snap (static screenshot after command completes).
    /// </summary>
    public static List<ICommand> BuildSnapCommands(DirectCaptureSettings settings, string outputPath)
    {
        var commands = new List<ICommand>();

        // Add Set commands for configuration
        AddSettingCommands(commands, settings);

        // Add the Exec command to run the shell command
        commands.Add(new ExecCommand(settings.Command));

        // Add Screenshot command to capture final frame as SVG
        commands.Add(new ScreenshotCommand(outputPath));

        return commands;
    }

    /// <summary>
    /// Builds a command list for a capture (animated recording of command output).
    /// </summary>
    public static List<ICommand> BuildCaptureCommands(DirectCaptureSettings settings, string outputPath)
    {
        var commands = new List<ICommand>();

        // Add Set commands for configuration
        AddSettingCommands(commands, settings);

        // Add Output command for animated SVG
        commands.Add(new OutputCommand(outputPath));

        // Add the Exec command to run the shell command
        commands.Add(new ExecCommand(settings.Command));

        return commands;
    }

    private static void AddSettingCommands(List<ICommand> commands, DirectCaptureSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.Theme))
        {
            commands.Add(new SetCommand("Theme", settings.Theme));
        }

        if (settings.Cols.HasValue)
        {
            commands.Add(new SetCommand("Cols", settings.Cols.Value));
        }

        if (settings.Rows.HasValue)
        {
            commands.Add(new SetCommand("Rows", settings.Rows.Value));
        }

        if (settings.FontSize.HasValue)
        {
            commands.Add(new SetCommand("FontSize", settings.FontSize.Value));
        }

        if (settings.DisableCursor)
        {
            commands.Add(new SetCommand("DisableCursor", true));
        }

        if (settings.TransparentBackground)
        {
            commands.Add(new SetCommand("TransparentBackground", true));
        }

        if (!string.IsNullOrWhiteSpace(settings.EndBuffer))
        {
            var endBuffer = ParseDuration(settings.EndBuffer);
            commands.Add(new SetCommand("EndBuffer", endBuffer));
        }
    }

    /// <summary>
    /// Parses a duration string like "500ms" or "2s" into a TimeSpan.
    /// </summary>
    public static TimeSpan ParseDuration(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Duration value cannot be empty", nameof(value));
        }

        value = value.Trim();

        // Support formats like "500ms", "1s", "1.5s"
        if (value.EndsWith("ms", StringComparison.OrdinalIgnoreCase))
        {
            var ms = double.Parse(value[..^2]);
            return TimeSpan.FromMilliseconds(ms);
        }

        if (value.EndsWith("s", StringComparison.OrdinalIgnoreCase))
        {
            var s = double.Parse(value[..^1]);
            return TimeSpan.FromSeconds(s);
        }

        // Assume milliseconds if no suffix
        return TimeSpan.FromMilliseconds(double.Parse(value));
    }
}
