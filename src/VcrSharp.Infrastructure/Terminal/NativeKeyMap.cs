namespace VcrSharp.Infrastructure.Terminal;

/// <summary>
/// Translates the browser-style key codes the tape commands emit (via <c>KeyMapper</c>) into the raw
/// byte sequences a real terminal sends to a shell over a PTY. This is the native counterpart to the
/// Playwright keyboard: where the browser path dispatches DOM key events to xterm.js, the native path
/// writes these bytes straight to the ConPTY stdin and the shell echoes them back through the parser.
/// </summary>
internal static class NativeKeyMap
{
    private const char Esc = (char)0x1b;

    /// <summary>Bytes for a single key press. <paramref name="key"/> is a browser-style code
    /// (e.g. "Enter", "ArrowUp", "F5") or a literal character to send as-is.</summary>
    public static string ForKey(string key) => key switch
    {
        "Enter" => "\r",
        "Tab" => "\t",
        "Escape" => Esc.ToString(),
        "Space" => " ",
        "Backspace" => ((char)0x7f).ToString(),
        "Delete" => $"{Esc}[3~",
        "ArrowUp" => $"{Esc}[A",
        "ArrowDown" => $"{Esc}[B",
        "ArrowRight" => $"{Esc}[C",
        "ArrowLeft" => $"{Esc}[D",
        "Home" => $"{Esc}[H",
        "End" => $"{Esc}[F",
        "PageUp" => $"{Esc}[5~",
        "PageDown" => $"{Esc}[6~",
        "Insert" => $"{Esc}[2~",
        "F1" => $"{Esc}OP",
        "F2" => $"{Esc}OQ",
        "F3" => $"{Esc}OR",
        "F4" => $"{Esc}OS",
        "F5" => $"{Esc}[15~",
        "F6" => $"{Esc}[17~",
        "F7" => $"{Esc}[18~",
        "F8" => $"{Esc}[19~",
        "F9" => $"{Esc}[20~",
        "F10" => $"{Esc}[21~",
        "F11" => $"{Esc}[23~",
        "F12" => $"{Esc}[24~",
        _ => key, // literal text (single characters, or unknown keys passed through)
    };

    /// <summary>Bytes for a modified key press (Ctrl/Alt/Shift + key).</summary>
    public static string ForCombination(IReadOnlyList<string> modifiers, string key)
    {
        var ctrl = modifiers.Contains("Control");
        var alt = modifiers.Contains("Alt");
        var shift = modifiers.Contains("Shift");

        if (shift && key == "Tab") return $"{Esc}[Z"; // back-tab (CBT)

        var seq = ForKey(key);

        if (ctrl && seq.Length == 1)
        {
            // Ctrl + ASCII → control code (Ctrl+@=0x00, Ctrl+A=0x01 … Ctrl+_=0x1f).
            var c = char.ToUpperInvariant(seq[0]);
            if (c is >= '@' and <= '_') seq = ((char)(c - '@')).ToString();
            else if (c == ' ') seq = "\0";
        }

        if (alt) seq = Esc + seq; // Alt prefixes ESC

        return seq;
    }
}
