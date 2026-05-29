using VcrSharp.Core.Session;

namespace VcrSharp.Core.Recording;

/// <summary>
/// Options that control how <see cref="InputToTapeConverter"/> turns captured input into tape text.
/// </summary>
public sealed class InputToTapeOptions
{
    /// <summary>
    /// Minimum pause between consecutive keystrokes that becomes a <c>Sleep</c> command.
    /// Gaps shorter than this are treated as normal typing and produce no Sleep.
    /// </summary>
    public TimeSpan SleepThreshold { get; init; } = TimeSpan.FromMilliseconds(300);

    /// <summary>The shortest <c>Sleep</c> duration that will be emitted (gaps are clamped up to this).</summary>
    public TimeSpan MinSleep { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// The longest <c>Sleep</c> duration that will be emitted. Long idle periods (e.g. the user
    /// stepping away) are clamped to this so a multi-minute pause is not baked into the tape.
    /// </summary>
    public TimeSpan MaxSleep { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The shell the session was recorded in. A <c>Set Shell "..."</c> line is emitted only when this
    /// is non-null and differs from <see cref="DefaultShell"/> (mirrors VHS behaviour).
    /// </summary>
    public string? Shell { get; init; }

    /// <summary>The platform default shell, used to decide whether to emit <c>Set Shell</c>.</summary>
    public string? DefaultShell { get; init; }

    /// <summary>
    /// Session options whose non-default values become header <c>Set</c> commands
    /// (Cols, Rows, FontSize, Theme).
    /// </summary>
    public SessionOptions? Header { get; init; }

    /// <summary>
    /// When true, a trailing shell-exit sequence (<c>exit</c> + Enter, or Ctrl+D) used to end the
    /// recording session is stripped from the generated tape.
    /// </summary>
    public bool StripExit { get; init; } = true;
}
