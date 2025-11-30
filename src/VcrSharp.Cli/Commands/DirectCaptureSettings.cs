using System.ComponentModel;
using Spectre.Console.Cli;

namespace VcrSharp.Cli.Commands;

/// <summary>
/// Shared settings for direct capture commands (snap and capture).
/// These settings map directly to Set commands in tape files.
/// </summary>
public abstract class DirectCaptureSettings : CommandSettings
{
    /// <summary>
    /// The shell command to execute.
    /// </summary>
    [CommandArgument(0, "<command>")]
    [Description("Shell command to execute")]
    public required string Command { get; init; }

    /// <summary>
    /// Output file path. Defaults to output.svg.
    /// </summary>
    [CommandOption("-o|--output <FILE>")]
    [Description("Output file path (default: output.svg)")]
    public string? Output { get; init; }

    /// <summary>
    /// Terminal theme name.
    /// </summary>
    [CommandOption("--theme <THEME>")]
    [Description("Terminal theme (e.g., 'Dracula', 'One Dark')")]
    public string? Theme { get; init; }

    /// <summary>
    /// Number of terminal columns.
    /// </summary>
    [CommandOption("--cols <COLS>")]
    [Description("Terminal width in columns")]
    public int? Cols { get; init; }

    /// <summary>
    /// Number of terminal rows.
    /// </summary>
    [CommandOption("--rows <ROWS>")]
    [Description("Terminal height in rows")]
    public int? Rows { get; init; }

    /// <summary>
    /// Font size in pixels.
    /// </summary>
    [CommandOption("--font-size <SIZE>")]
    [Description("Font size in pixels")]
    public int? FontSize { get; init; }

    /// <summary>
    /// Disable cursor rendering in output.
    /// </summary>
    [CommandOption("--disable-cursor")]
    [Description("Hide cursor in output")]
    public bool DisableCursor { get; init; }

    /// <summary>
    /// Enable transparent background.
    /// </summary>
    [CommandOption("--transparent-background")]
    [Description("Use transparent background")]
    public bool TransparentBackground { get; init; }

    /// <summary>
    /// Buffer time after last activity before stopping.
    /// </summary>
    [CommandOption("--end-buffer <DURATION>")]
    [Description("Buffer after last activity (e.g., '500ms', '2s')")]
    public string? EndBuffer { get; init; }

    /// <summary>
    /// Enable verbose logging.
    /// </summary>
    [CommandOption("-v|--verbose")]
    [Description("Enable verbose logging")]
    public bool Verbose { get; init; }
}
