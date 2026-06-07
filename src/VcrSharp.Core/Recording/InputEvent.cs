namespace VcrSharp.Core.Recording;

/// <summary>
/// A single captured terminal input event: one chunk read from the host stdin during recording.
/// </summary>
/// <param name="Data">
/// The raw byte/escape sequence the terminal sends to the shell for this keystroke, decoded to a
/// string (e.g. <c>"a"</c>, <c>"\r"</c> for Enter,
/// <c>"[A"</c> for the Up arrow, <c>""</c> for Ctrl+C). Because it is the *input*
/// stream rather than shell output, it is identical across shells (pwsh, cmd, bash, zsh, fish).
/// </param>
/// <param name="Timestamp">When the event arrived, measured from the start of input capture.</param>
public readonly record struct InputEvent(string Data, TimeSpan Timestamp);
