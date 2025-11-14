using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using VcrSharp.Core.Parsing.Ast;
using VcrSharp.Core.Settings;

namespace VcrSharp.Core.Session;

/// <summary>
/// Configuration options for a VCR recording session.
/// </summary>
public class SessionOptions
{
    /// <summary>
    /// Gets the default shell for the current platform.
    /// </summary>
    private static readonly string DefaultShellValue = GetPlatformDefaultShell();

    // Terminal Dimensions

    /// <summary>
    /// Gets or sets the terminal width in pixels.
    /// Only used if Cols is not specified.
    /// </summary>
    public int Width { get; set; } = 1200;

    /// <summary>
    /// Gets or sets the terminal height in pixels.
    /// Only used if Rows is not specified.
    /// </summary>
    public int Height { get; set; } = 600;

    /// <summary>
    /// Gets or sets the number of terminal columns (character width).
    /// When specified, overrides Width and auto-calculates viewport dimensions.
    /// </summary>
    public int? Cols { get; set; }

    /// <summary>
    /// Gets or sets the number of terminal rows (character height).
    /// When specified, overrides Height and auto-calculates viewport dimensions.
    /// </summary>
    public int? Rows { get; set; }

    // Font Settings

    /// <summary>
    /// Gets or sets the font size in pixels.
    /// </summary>
    public int FontSize { get; set; } = 22;

    /// <summary>
    /// Gets or sets the font family name.
    /// </summary>
    public string FontFamily { get; set; } = "monospace";

    /// <summary>
    /// Gets or sets the letter spacing multiplier.
    /// </summary>
    public float LetterSpacing { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the line height multiplier.
    /// </summary>
    public float LineHeight { get; set; } = 1.0f;

    // Video Settings

    /// <summary>
    /// Gets or sets the recording framerate (1-120 fps).
    /// </summary>
    public int Framerate { get; set; } = 50;

    /// <summary>
    /// Gets or sets the playback speed multiplier.
    /// </summary>
    public float PlaybackSpeed { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the loop offset in seconds (for GIFs).
    /// </summary>
    public float LoopOffset { get; set; }

    /// <summary>
    /// Gets or sets the maximum colors for GIF palette generation (1-256).
    /// </summary>
    public int MaxColors { get; set; } = 256;

    // Styling

    /// <summary>
    /// Gets or sets the terminal theme.
    /// </summary>
    public Theme Theme { get; set; } = BuiltinThemes.Default;

    /// <summary>
    /// Gets or sets the padding in pixels around the terminal.
    /// </summary>
    public int Padding { get; set; }

    /// <summary>
    /// Gets or sets the margin in pixels around the recording.
    /// </summary>
    public int Margin { get; set; }

    /// <summary>
    /// Gets or sets the margin fill color or image path.
    /// </summary>
    public string? MarginFill { get; set; }

    /// <summary>
    /// Gets or sets the window bar size in pixels.
    /// </summary>
    public int WindowBarSize { get; set; } = 30;

    /// <summary>
    /// Gets or sets the border radius in pixels.
    /// </summary>
    public int BorderRadius { get; set; }

    /// <summary>
    /// Gets or sets whether the cursor should blink.
    /// </summary>
    public bool CursorBlink { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the terminal background should be transparent.
    /// When true, sets allowTransparency on xTerm.js and uses a transparent background color.
    /// </summary>
    public bool TransparentBackground { get; set; }

    // Behavior

    /// <summary>
    /// Gets or sets the shell to use for the session.
    /// Defaults to platform-appropriate shell: pwsh/powershell/cmd on Windows, bash on Unix.
    /// </summary>
    public string Shell { get; set; } = DefaultShellValue;

    /// <summary>
    /// Gets or sets the working directory for the terminal session.
    /// If not specified, defaults to the current directory where the command is run.
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Gets or sets the typing speed delay between characters.
    /// </summary>
    public TimeSpan TypingSpeed { get; set; } = TimeSpan.FromMilliseconds(60);

    /// <summary>
    /// Gets or sets the timeout for Wait commands.
    /// </summary>
    public TimeSpan WaitTimeout { get; set; } = TimeSpan.FromSeconds(15);

    private Regex? _waitPattern;

    /// <summary>
    /// Gets or sets the regex pattern to detect shell prompt completion.
    /// If not explicitly set, uses the default pattern from the shell configuration.
    /// </summary>
    public Regex WaitPattern
    {
        get => _waitPattern ?? ShellConfiguration.GetConfiguration(Shell).DefaultPromptPattern;
        set => _waitPattern = value;
    }

    /// <summary>
    /// Gets or sets the inactivity timeout for detecting command completion.
    /// If terminal output doesn't change for this duration, command is considered complete.
    /// </summary>
    public TimeSpan InactivityTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the maximum time to wait for first terminal activity.
    /// Recording will start when the first buffer change is detected or this timeout is reached.
    /// </summary>
    public TimeSpan StartWaitTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the amount of blank time to include before the first activity.
    /// Frames captured before (FirstActivity - StartBuffer) will be trimmed.
    /// </summary>
    public TimeSpan StartBuffer { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Gets or sets the amount of time to include after the last detected activity.
    /// Recording will continue for this duration after the last buffer change.
    /// </summary>
    public TimeSpan EndBuffer { get; set; } = TimeSpan.FromMilliseconds(100);

    // Output

    /// <summary>
    /// Gets or sets the list of output file paths to generate.
    /// </summary>
    public List<string> OutputFiles { get; set; } = new();

    /// <summary>
    /// Gets or sets environment variables to set in the shell.
    /// </summary>
    public Dictionary<string, string> Environment { get; set; } = new();

    /// <summary>
    /// Validates the session options.
    /// </summary>
    /// <returns>A list of validation error messages, or empty if valid.</returns>
    public List<string> Validate()
    {
        var errors = new List<string>();

        if (Cols.HasValue && Cols.Value <= 0)
            errors.Add("Cols must be greater than 0");

        if (Rows.HasValue && Rows.Value <= 0)
            errors.Add("Rows must be greater than 0");

        if (!Cols.HasValue && Width <= 0)
            errors.Add("Width must be greater than 0");

        if (!Rows.HasValue && Height <= 0)
            errors.Add("Height must be greater than 0");

        if (FontSize <= 0)
            errors.Add("FontSize must be greater than 0");

        if (Framerate < 1 || Framerate > 120)
            errors.Add("Framerate must be between 1 and 120");

        if (PlaybackSpeed <= 0)
            errors.Add("PlaybackSpeed must be greater than 0");

        if (MaxColors < 1 || MaxColors > 256)
            errors.Add("MaxColors must be between 1 and 256");

        if (Padding < 0)
            errors.Add("Padding must be non-negative");

        if (Margin < 0)
            errors.Add("Margin must be non-negative");

        if (BorderRadius < 0)
            errors.Add("BorderRadius must be non-negative");

        if (WindowBarSize <= 0)
            errors.Add("WindowBarSize must be greater than 0");

        if (string.IsNullOrWhiteSpace(Shell))
            errors.Add("Shell must be specified");

        if (!string.IsNullOrWhiteSpace(WorkingDirectory) && !Directory.Exists(WorkingDirectory))
            errors.Add($"WorkingDirectory does not exist: {WorkingDirectory}");

        if (string.IsNullOrWhiteSpace(FontFamily))
            errors.Add("FontFamily must be specified");

        if (OutputFiles.Count == 0)
            errors.Add("At least one output file must be specified");

        return errors;
    }

    /// <summary>
    /// Creates SessionOptions from a list of parsed commands.
    /// Extracts Set, Output, and Env commands to build the configuration.
    /// </summary>
    public static SessionOptions FromCommands(List<ICommand> commands)
    {
        var options = new SessionOptions();

        foreach (var command in commands)
        {
            switch (command)
            {
                case SetCommand set:
                    ApplySetting(options, set.SettingName, set.Value);
                    break;

                case OutputCommand output:
                    options.OutputFiles.Add(output.FilePath);
                    break;

                case EnvCommand env:
                    options.Environment[env.Key] = env.Value;
                    break;
            }
        }

        return options;
    }

    /// <summary>
    /// Applies a setting value to the options object.
    /// </summary>
    private static void ApplySetting(SessionOptions options, string name, object value)
    {
        switch (name.ToLowerInvariant())
        {
            // Terminal dimensions
            case "width":
                options.Width = Convert.ToInt32(value);
                break;
            case "height":
                options.Height = Convert.ToInt32(value);
                break;
            case "cols":
                options.Cols = Convert.ToInt32(value);
                break;
            case "rows":
                options.Rows = Convert.ToInt32(value);
                break;

            // Font settings
            case "fontsize":
                options.FontSize = Convert.ToInt32(value);
                break;
            case "fontfamily":
                options.FontFamily = value.ToString() ?? "monospace";
                break;
            case "letterspacing":
                options.LetterSpacing = Convert.ToSingle(value);
                break;
            case "lineheight":
                options.LineHeight = Convert.ToSingle(value);
                break;

            // Video settings
            case "framerate":
                options.Framerate = Convert.ToInt32(value);
                break;
            case "playbackspeed":
                options.PlaybackSpeed = Convert.ToSingle(value);
                break;
            case "loopoffset":
                options.LoopOffset = Convert.ToSingle(value);
                break;
            case "maxcolors":
                options.MaxColors = Convert.ToInt32(value);
                break;

            // Styling
            case "theme":
                var themeName = value.ToString() ?? "Default";
                options.Theme = BuiltinThemes.GetByName(themeName) ?? BuiltinThemes.Default;
                break;
            case "padding":
                options.Padding = Convert.ToInt32(value);
                break;
            case "margin":
                options.Margin = Convert.ToInt32(value);
                break;
            case "marginfill":
                options.MarginFill = value.ToString();
                break;
            case "windowbarsize":
                options.WindowBarSize = Convert.ToInt32(value);
                break;
            case "borderradius":
                options.BorderRadius = Convert.ToInt32(value);
                break;
            case "cursorblink":
                options.CursorBlink = Convert.ToBoolean(value);
                break;
            case "transparentbackground":
                options.TransparentBackground = Convert.ToBoolean(value);
                break;

            // Behavior
            case "shell":
                options.Shell = value.ToString() ?? "bash";
                break;
            case "workingdirectory":
                options.WorkingDirectory = value.ToString();
                break;
            case "typingspeed":
                if (value is TimeSpan ts)
                    options.TypingSpeed = ts;
                else
                {
                    var strValue = value.ToString() ?? "50ms";
                    // If value is just a number (e.g., "10"), treat it as milliseconds
                    if (int.TryParse(strValue, out var milliseconds))
                        options.TypingSpeed = TimeSpan.FromMilliseconds(milliseconds);
                    else
                        options.TypingSpeed = TimeSpan.Parse(strValue);
                }
                break;
            case "waittimeout":
                if (value is TimeSpan wt)
                    options.WaitTimeout = wt;
                else
                    options.WaitTimeout = TimeSpan.Parse(value.ToString() ?? "15s");
                break;
            case "waitpattern":
                if (value is Regex regex)
                    options.WaitPattern = regex;
                else
                    options.WaitPattern = new Regex(value.ToString() ?? "/>$/");
                break;
            case "inactivitytimeout":
                if (value is TimeSpan it)
                    options.InactivityTimeout = it;
                else
                    options.InactivityTimeout = TimeSpan.Parse(value.ToString() ?? "5s");
                break;
            case "startwaittimeout":
                if (value is TimeSpan swt)
                    options.StartWaitTimeout = swt;
                else
                    options.StartWaitTimeout = TimeSpan.Parse(value.ToString() ?? "10s");
                break;
            case "startbuffer":
                if (value is TimeSpan sb)
                    options.StartBuffer = sb;
                else
                    options.StartBuffer = TimeSpan.Parse(value.ToString() ?? "500ms");
                break;
            case "endbuffer":
                if (value is TimeSpan eb)
                    options.EndBuffer = eb;
                else
                    options.EndBuffer = TimeSpan.Parse(value.ToString() ?? "1s");
                break;
        }
    }

    /// <summary>
    /// Gets the default shell for the current platform.
    /// On Windows: tries pwsh, then powershell, then cmd.
    /// On Unix/Mac: returns bash.
    /// </summary>
    private static string GetPlatformDefaultShell()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Try PowerShell Core first, fallback to Windows PowerShell, then CMD
            if (IsCommandAvailable("pwsh"))
                return "pwsh";
            if (IsCommandAvailable("powershell"))
                return "powershell";
            return "cmd";
        }

        // Unix-like systems (Linux, macOS)
        return "bash";
    }

    /// <summary>
    /// Checks if a command is available in the system PATH.
    /// </summary>
    private static bool IsCommandAvailable(string command)
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which",
                Arguments = command,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null) return false;

            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}