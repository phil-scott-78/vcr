namespace VcrSharp.Core.Settings;

/// <summary>
/// Provides built-in terminal themes.
/// </summary>
public static class BuiltinThemes
{
    /// <summary>
    /// Gets the default Visual Studio Code Dark+ theme.
    /// </summary>
    public static Theme Default => new()
    {
        Name = "Default",
        Background = "#1e1e1e",
        Foreground = "#d4d4d4",
        Cursor = "#d4d4d4",
        SelectionBackground = "#264f78",
        Black = "#000000",
        Red = "#cd3131",
        Green = "#0dbc79",
        Yellow = "#e5e510",
        Blue = "#2472c8",
        Magenta = "#bc3fbc",
        Cyan = "#11a8cd",
        White = "#e5e5e5",
        BrightBlack = "#666666",
        BrightRed = "#f14c4c",
        BrightGreen = "#23d18b",
        BrightYellow = "#f5f543",
        BrightBlue = "#3b8eea",
        BrightMagenta = "#d670d6",
        BrightCyan = "#29b8db",
        BrightWhite = "#ffffff"
    };

    /// <summary>
    /// Gets the Dracula theme.
    /// </summary>
    public static Theme Dracula => new()
    {
        Name = "Dracula",
        Background = "#282a36",
        Foreground = "#f8f8f2",
        Cursor = "#f8f8f2",
        SelectionBackground = "#44475a",
        Black = "#21222c",
        Red = "#ff5555",
        Green = "#50fa7b",
        Yellow = "#f1fa8c",
        Blue = "#bd93f9",
        Magenta = "#ff79c6",
        Cyan = "#8be9fd",
        White = "#f8f8f2",
        BrightBlack = "#6272a4",
        BrightRed = "#ff6e6e",
        BrightGreen = "#69ff94",
        BrightYellow = "#ffffa5",
        BrightBlue = "#d6acff",
        BrightMagenta = "#ff92df",
        BrightCyan = "#a4ffff",
        BrightWhite = "#ffffff"
    };

    /// <summary>
    /// Gets the Monokai theme.
    /// </summary>
    public static Theme Monokai => new()
    {
        Name = "Monokai",
        Background = "#272822",
        Foreground = "#f8f8f2",
        Cursor = "#f8f8f0",
        SelectionBackground = "#49483e",
        Black = "#272822",
        Red = "#f92672",
        Green = "#a6e22e",
        Yellow = "#f4bf75",
        Blue = "#66d9ef",
        Magenta = "#ae81ff",
        Cyan = "#a1efe4",
        White = "#f8f8f2",
        BrightBlack = "#75715e",
        BrightRed = "#f92672",
        BrightGreen = "#a6e22e",
        BrightYellow = "#f4bf75",
        BrightBlue = "#66d9ef",
        BrightMagenta = "#ae81ff",
        BrightCyan = "#a1efe4",
        BrightWhite = "#f9f8f5"
    };

    /// <summary>
    /// Gets the Nord theme.
    /// </summary>
    public static Theme Nord => new()
    {
        Name = "Nord",
        Background = "#2e3440",
        Foreground = "#d8dee9",
        Cursor = "#d8dee9",
        SelectionBackground = "#434c5e",
        Black = "#3b4252",
        Red = "#bf616a",
        Green = "#a3be8c",
        Yellow = "#ebcb8b",
        Blue = "#81a1c1",
        Magenta = "#b48ead",
        Cyan = "#88c0d0",
        White = "#e5e9f0",
        BrightBlack = "#4c566a",
        BrightRed = "#bf616a",
        BrightGreen = "#a3be8c",
        BrightYellow = "#ebcb8b",
        BrightBlue = "#81a1c1",
        BrightMagenta = "#b48ead",
        BrightCyan = "#8fbcbb",
        BrightWhite = "#eceff4"
    };

    /// <summary>
    /// Gets the Solarized Dark theme.
    /// </summary>
    public static Theme SolarizedDark => new()
    {
        Name = "Solarized Dark",
        Background = "#002b36",
        Foreground = "#839496",
        Cursor = "#839496",
        SelectionBackground = "#073642",
        Black = "#073642",
        Red = "#dc322f",
        Green = "#859900",
        Yellow = "#b58900",
        Blue = "#268bd2",
        Magenta = "#d33682",
        Cyan = "#2aa198",
        White = "#eee8d5",
        BrightBlack = "#002b36",
        BrightRed = "#cb4b16",
        BrightGreen = "#586e75",
        BrightYellow = "#657b83",
        BrightBlue = "#839496",
        BrightMagenta = "#6c71c4",
        BrightCyan = "#93a1a1",
        BrightWhite = "#fdf6e3"
    };

    /// <summary>
    /// Gets the Solarized Light theme.
    /// </summary>
    public static Theme SolarizedLight => new()
    {
        Name = "Solarized Light",
        Background = "#fdf6e3",
        Foreground = "#657b83",
        Cursor = "#657b83",
        SelectionBackground = "#eee8d5",
        Black = "#073642",
        Red = "#dc322f",
        Green = "#859900",
        Yellow = "#b58900",
        Blue = "#268bd2",
        Magenta = "#d33682",
        Cyan = "#2aa198",
        White = "#eee8d5",
        BrightBlack = "#002b36",
        BrightRed = "#cb4b16",
        BrightGreen = "#586e75",
        BrightYellow = "#657b83",
        BrightBlue = "#839496",
        BrightMagenta = "#6c71c4",
        BrightCyan = "#93a1a1",
        BrightWhite = "#fdf6e3"
    };

    /// <summary>
    /// Gets the One Dark theme.
    /// </summary>
    public static Theme OneDark => new()
    {
        Name = "One Dark",
        Background = "#282c34",
        Foreground = "#abb2bf",
        Cursor = "#528bff",
        SelectionBackground = "#3e4451",
        Black = "#282c34",
        Red = "#e06c75",
        Green = "#98c379",
        Yellow = "#e5c07b",
        Blue = "#61afef",
        Magenta = "#c678dd",
        Cyan = "#56b6c2",
        White = "#abb2bf",
        BrightBlack = "#5c6370",
        BrightRed = "#e06c75",
        BrightGreen = "#98c379",
        BrightYellow = "#e5c07b",
        BrightBlue = "#61afef",
        BrightMagenta = "#c678dd",
        BrightCyan = "#56b6c2",
        BrightWhite = "#ffffff"
    };

    /// <summary>
    /// Gets the Gruvbox Dark theme.
    /// </summary>
    public static Theme GruvboxDark => new()
    {
        Name = "Gruvbox Dark",
        Background = "#282828",
        Foreground = "#ebdbb2",
        Cursor = "#ebdbb2",
        SelectionBackground = "#504945",
        Black = "#282828",
        Red = "#cc241d",
        Green = "#98971a",
        Yellow = "#d79921",
        Blue = "#458588",
        Magenta = "#b16286",
        Cyan = "#689d6a",
        White = "#a89984",
        BrightBlack = "#928374",
        BrightRed = "#fb4934",
        BrightGreen = "#b8bb26",
        BrightYellow = "#fabd2f",
        BrightBlue = "#83a598",
        BrightMagenta = "#d3869b",
        BrightCyan = "#8ec07c",
        BrightWhite = "#ebdbb2"
    };

    /// <summary>
    /// Gets the Tokyo Night theme.
    /// </summary>
    public static Theme TokyoNight => new()
    {
        Name = "Tokyo Night",
        Background = "#1a1b26",
        Foreground = "#c0caf5",
        Cursor = "#c0caf5",
        SelectionBackground = "#283457",
        Black = "#15161e",
        Red = "#f7768e",
        Green = "#9ece6a",
        Yellow = "#e0af68",
        Blue = "#7aa2f7",
        Magenta = "#bb9af7",
        Cyan = "#7dcfff",
        White = "#a9b1d6",
        BrightBlack = "#414868",
        BrightRed = "#f7768e",
        BrightGreen = "#9ece6a",
        BrightYellow = "#e0af68",
        BrightBlue = "#7aa2f7",
        BrightMagenta = "#bb9af7",
        BrightCyan = "#7dcfff",
        BrightWhite = "#c0caf5"
    };

    /// <summary>
    /// Gets the Catppuccin Mocha theme.
    /// </summary>
    public static Theme CatppuccinMocha => new()
    {
        Name = "Catppuccin Mocha",
        Background = "#1e1e2e",
        Foreground = "#cdd6f4",
        Cursor = "#f5e0dc",
        SelectionBackground = "#585b70",
        Black = "#45475a",
        Red = "#f38ba8",
        Green = "#a6e3a1",
        Yellow = "#f9e2af",
        Blue = "#89b4fa",
        Magenta = "#f5c2e7",
        Cyan = "#94e2d5",
        White = "#bac2de",
        BrightBlack = "#585b70",
        BrightRed = "#f38ba8",
        BrightGreen = "#a6e3a1",
        BrightYellow = "#f9e2af",
        BrightBlue = "#89b4fa",
        BrightMagenta = "#f5c2e7",
        BrightCyan = "#94e2d5",
        BrightWhite = "#a6adc8"
    };

    /// <summary>
    /// Gets all built-in themes.
    /// </summary>
    public static IReadOnlyList<Theme> All =>
    [
        Default,
        Dracula,
        Monokai,
        Nord,
        SolarizedDark,
        SolarizedLight,
        OneDark,
        GruvboxDark,
        TokyoNight,
        CatppuccinMocha
    ];

    /// <summary>
    /// Gets a theme by name (case-insensitive).
    /// </summary>
    /// <param name="name">The theme name.</param>
    /// <returns>The theme, or null if not found.</returns>
    public static Theme? GetByName(string name)
    {
        return All.FirstOrDefault(t =>
            t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}