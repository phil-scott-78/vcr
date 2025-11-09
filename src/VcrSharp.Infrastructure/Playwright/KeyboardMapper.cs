using System.Runtime.InteropServices;

namespace VcrSharp.Infrastructure.Playwright;

/// <summary>
/// Maps VHS tape file key names to Playwright keyboard keys.
/// </summary>
public static class KeyboardMapper
{
    /// <summary>
    /// Maps a key name from a tape file command to a Playwright key string.
    /// </summary>
    /// <param name="keyName">The key name from the tape file (e.g., "Enter", "Backspace", "Up").</param>
    /// <returns>The Playwright key string, or null if not recognized.</returns>
    public static string? MapKey(string keyName)
    {
        if (string.IsNullOrWhiteSpace(keyName))
        {
            return null;
        }

        // Normalize the key name
        var normalized = keyName.Trim();

        // Check special keys mapping
        return normalized switch
        {
            // Navigation keys
            "Enter" => "Enter",
            "Return" => "Enter",
            "Tab" => "Tab",
            "Escape" => "Escape",
            "Esc" => "Escape",
            "Space" => "Space",

            // Editing keys
            "Backspace" => "Backspace",
            "Delete" => "Delete",
            "Del" => "Delete",

            // Arrow keys
            "Up" => "ArrowUp",
            "Down" => "ArrowDown",
            "Left" => "ArrowLeft",
            "Right" => "ArrowRight",

            // Home/End/Page keys
            "Home" => "Home",
            "End" => "End",
            "PageUp" => "PageUp",
            "PageDown" => "PageDown",

            // Function keys
            "F1" => "F1",
            "F2" => "F2",
            "F3" => "F3",
            "F4" => "F4",
            "F5" => "F5",
            "F6" => "F6",
            "F7" => "F7",
            "F8" => "F8",
            "F9" => "F9",
            "F10" => "F10",
            "F11" => "F11",
            "F12" => "F12",

            // Insert
            "Insert" => "Insert",
            "Ins" => "Insert",

            // Single characters are returned as-is
            _ when normalized.Length == 1 => normalized,

            // Not recognized
            _ => null
        };
    }

    /// <summary>
    /// Maps a modifier name to a Playwright modifier key string.
    /// </summary>
    /// <param name="modifierName">The modifier name (e.g., "Ctrl", "Alt", "Shift").</param>
    /// <returns>The Playwright modifier key string, or null if not recognized.</returns>
    public static string? MapModifier(string modifierName)
    {
        if (string.IsNullOrWhiteSpace(modifierName))
        {
            return null;
        }

        var normalized = modifierName.Trim();

        return normalized switch
        {
            "Ctrl" => "Control",
            "Control" => "Control",
            "Alt" => "Alt",
            "Shift" => "Shift",
            "Meta" => "Meta",
            "Cmd" => "Meta",
            "Command" => "Meta",
            "Super" => "Meta",
            "Win" => "Meta",
            _ => null
        };
    }

    /// <summary>
    /// Determines if a key name represents a modifier key.
    /// </summary>
    /// <param name="keyName">The key name to check.</param>
    /// <returns>True if the key is a modifier, false otherwise.</returns>
    public static bool IsModifier(string keyName)
    {
        return MapModifier(keyName) != null;
    }

    /// <summary>
    /// Gets the platform-appropriate "Ctrl" modifier (Control on Windows/Linux, Meta on macOS).
    /// </summary>
    /// <returns>"Meta" on macOS, "Control" otherwise.</returns>
    public static string GetPlatformCtrlKey()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "Meta" : "Control";
    }

    /// <summary>
    /// Parses a key combination string (e.g., "Ctrl+C", "Alt+Shift+Tab") into modifiers and a key.
    /// </summary>
    /// <param name="combination">The key combination string.</param>
    /// <returns>A tuple of (modifiers list, main key), or null if invalid.</returns>
    public static (List<string> Modifiers, string Key)? ParseKeyCombination(string combination)
    {
        if (string.IsNullOrWhiteSpace(combination))
        {
            return null;
        }

        var parts = combination.Split('+', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .ToList();

        if (parts.Count == 0)
        {
            return null;
        }

        // Last part is the main key
        var mainKey = parts[^1];
        var mappedKey = MapKey(mainKey);

        if (mappedKey == null)
        {
            return null;
        }

        // All other parts are modifiers
        var modifiers = new List<string>();
        for (var i = 0; i < parts.Count - 1; i++)
        {
            var mappedModifier = MapModifier(parts[i]);
            if (mappedModifier == null)
            {
                return null; // Invalid modifier
            }
            modifiers.Add(mappedModifier);
        }

        return (modifiers, mappedKey);
    }

    /// <summary>
    /// Gets all supported key names for validation purposes.
    /// </summary>
    /// <returns>A list of all recognized key names.</returns>
    public static List<string> GetSupportedKeys()
    {
        return
        [
            "Enter", "Return", "Tab", "Escape", "Esc", "Space",

            // Editing
            "Backspace", "Delete", "Del",

            // Arrows
            "Up", "Down", "Left", "Right",

            // Home/End/Page
            "Home", "End", "PageUp", "PageDown",

            // Function keys
            "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",

            // Insert
            "Insert", "Ins"
        ];
    }

    /// <summary>
    /// Gets all supported modifier names for validation purposes.
    /// </summary>
    /// <returns>A list of all recognized modifier names.</returns>
    public static List<string> GetSupportedModifiers()
    {
        return ["Ctrl", "Control", "Alt", "Shift", "Meta", "Cmd", "Command", "Super", "Win"];
    }
}