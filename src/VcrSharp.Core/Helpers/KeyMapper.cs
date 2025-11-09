namespace VcrSharp.Core.Helpers;

/// <summary>
/// Maps tape file key names to their runtime equivalents.
/// Implementation is delegated to Infrastructure layer via dynamic dispatch.
/// </summary>
public static class KeyMapper
{
    /// <summary>
    /// Maps a key name from tape file to runtime key code.
    /// Uses pass-through mapping - actual implementation in Infrastructure.
    /// </summary>
    public static string MapKey(string keyName)
    {
        // Simple mappings that don't require Infrastructure
        return keyName switch
        {
            "Enter" => "Enter",
            "Tab" => "Tab",
            "Escape" => "Escape",
            "Space" => "Space",
            "Backspace" => "Backspace",
            "Delete" => "Delete",
            "Up" => "ArrowUp",
            "Down" => "ArrowDown",
            "Left" => "ArrowLeft",
            "Right" => "ArrowRight",
            "Home" => "Home",
            "End" => "End",
            "PageUp" => "PageUp",
            "PageDown" => "PageDown",
            "Insert" => "Insert",
            _ when keyName.StartsWith("F") && int.TryParse(keyName[1..], out var num) && num is >= 1 and <= 12
                => keyName, // F1-F12 pass through
            _ => keyName // Pass through other keys as-is
        };
    }

    /// <summary>
    /// Maps a character to its physical key code and determines if Shift is required.
    /// This is needed for modifier combinations (e.g., Alt+# should become Alt+Shift+Digit3).
    /// </summary>
    /// <param name="character">The character to map (e.g., "#", "A", "3")</param>
    /// <returns>Tuple of (physical key code, requires shift)</returns>
    public static (string KeyCode, bool RequiresShift) MapCharacterToPhysicalKey(string character)
    {
        // Handle single character strings
        if (character.Length != 1)
        {
            // Not a character, return as-is without shift
            return (character, false);
        }

        var ch = character[0];

        // Map special characters that require Shift (US keyboard layout)
        return ch switch
        {
            // Shifted digit row
            '!' => ("Digit1", true),
            '@' => ("Digit2", true),
            '#' => ("Digit3", true),
            '$' => ("Digit4", true),
            '%' => ("Digit5", true),
            '^' => ("Digit6", true),
            '&' => ("Digit7", true),
            '*' => ("Digit8", true),
            '(' => ("Digit9", true),
            ')' => ("Digit0", true),

            // Unshifted digits
            '0' => ("Digit0", false),
            '1' => ("Digit1", false),
            '2' => ("Digit2", false),
            '3' => ("Digit3", false),
            '4' => ("Digit4", false),
            '5' => ("Digit5", false),
            '6' => ("Digit6", false),
            '7' => ("Digit7", false),
            '8' => ("Digit8", false),
            '9' => ("Digit9", false),

            // Uppercase letters (require Shift)
            >= 'A' and <= 'Z' => ($"Key{ch}", true),

            // Lowercase letters (no Shift)
            >= 'a' and <= 'z' => ($"Key{char.ToUpper(ch)}", false),

            // Other shifted characters
            '~' => ("Backquote", true),
            '_' => ("Minus", true),
            '+' => ("Equal", true),
            '{' => ("BracketLeft", true),
            '}' => ("BracketRight", true),
            '|' => ("Backslash", true),
            ':' => ("Semicolon", true),
            '"' => ("Quote", true),
            '<' => ("Comma", true),
            '>' => ("Period", true),
            '?' => ("Slash", true),

            // Unshifted special characters
            '`' => ("Backquote", false),
            '-' => ("Minus", false),
            '=' => ("Equal", false),
            '[' => ("BracketLeft", false),
            ']' => ("BracketRight", false),
            '\\' => ("Backslash", false),
            ';' => ("Semicolon", false),
            '\'' => ("Quote", false),
            ',' => ("Comma", false),
            '.' => ("Period", false),
            '/' => ("Slash", false),

            // Default: return as-is
            _ => (character, false)
        };
    }

    /// <summary>
    /// Maps a modifier name to runtime modifier code.
    /// </summary>
    public static string MapModifier(string modifierName)
    {
        return modifierName switch
        {
            "Ctrl" => "Control",
            "Alt" => "Alt",
            "Shift" => "Shift",
            "Cmd" => "Meta",
            "Meta" => "Meta",
            _ => modifierName
        };
    }
}