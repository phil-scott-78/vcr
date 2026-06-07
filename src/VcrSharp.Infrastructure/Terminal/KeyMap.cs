namespace VcrSharp.Infrastructure.Terminal;

/// <summary>
/// Translates the key codes the tape commands emit (via <c>KeyMapper</c>) into the raw byte sequences a
/// real terminal sends to a shell over a PTY: it writes these bytes straight to the PTY stdin and the
/// shell echoes them back through the parser.
/// </summary>
internal static class KeyMap
{
    private const char Esc = (char)0x1b;

    /// <summary>Bytes for a single key press. <paramref name="key"/> is a key name
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

        // Named special keys (arrows/Home/End/Delete/PageUp-Down/Insert/function keys) carry their
        // modifiers as an xterm CSI parameter — e.g. Ctrl+Right is ESC[1;5C, not ESC ESC[C. Without this,
        // the modifier was silently dropped (single-char ctrl-folding/alt-prefix only worked for plain keys).
        if (ctrl || alt || shift)
        {
            var modified = ModifiedSpecialKey(key, ctrl, alt, shift);
            if (modified != null) return modified;
        }

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

    /// <summary>
    /// Builds the xterm-style modified sequence for a named special key, or null if <paramref name="key"/>
    /// is not a CSI/SS3 special key (so the caller falls back to the plain ctrl-fold / alt-prefix path).
    /// The modifier parameter is 1 + Shift(1) + Alt(2) + Ctrl(4).
    /// </summary>
    private static string? ModifiedSpecialKey(string key, bool ctrl, bool alt, bool shift)
    {
        var mod = 1 + (shift ? 1 : 0) + (alt ? 2 : 0) + (ctrl ? 4 : 0);

        // CSI <final> keys → ESC[1;<mod><final>
        var final = key switch
        {
            "ArrowUp" => 'A', "ArrowDown" => 'B', "ArrowRight" => 'C', "ArrowLeft" => 'D',
            "Home" => 'H', "End" => 'F',
            _ => '\0'
        };
        if (final != '\0') return $"{Esc}[1;{mod}{final}";

        // CSI <num> ~ keys → ESC[<num>;<mod>~
        var num = key switch
        {
            "Insert" => 2, "Delete" => 3, "PageUp" => 5, "PageDown" => 6,
            "F5" => 15, "F6" => 17, "F7" => 18, "F8" => 19, "F9" => 20,
            "F10" => 21, "F11" => 23, "F12" => 24,
            _ => 0
        };
        if (num != 0) return $"{Esc}[{num};{mod}~";

        // SS3 function keys F1–F4 take the CSI modified form → ESC[1;<mod>{P,Q,R,S}
        var ss3 = key switch { "F1" => 'P', "F2" => 'Q', "F3" => 'R', "F4" => 'S', _ => '\0' };
        if (ss3 != '\0') return $"{Esc}[1;{mod}{ss3}";

        return null;
    }
}
