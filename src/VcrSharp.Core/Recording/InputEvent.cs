namespace VcrSharp.Core.Recording;

/// <summary>
/// A single captured terminal input event, as produced by xterm.js's <c>onData</c> callback.
/// </summary>
/// <param name="Data">
/// The raw byte/escape sequence the terminal would send to the backend for this keystroke.
/// xterm.js delivers this as an already-decoded string (e.g. <c>"a"</c>, <c>"\r"</c> for Enter,
/// <c>"[A"</c> for the Up arrow, <c>""</c> for Ctrl+C). Because it is the *input*
/// stream rather than shell output, it is identical across shells (pwsh, cmd, bash, zsh, fish).
/// </param>
/// <param name="Timestamp">When the event arrived, measured from the start of input capture.</param>
public readonly record struct InputEvent(string Data, TimeSpan Timestamp);
