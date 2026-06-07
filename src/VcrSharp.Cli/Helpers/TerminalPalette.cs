using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace VcrSharp.Cli.Helpers;

/// <summary>
/// Queries the terminal for its current color palette RGB values.
/// </summary>
public static class TerminalPalette
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    private const int STD_INPUT_HANDLE = -10;
    private const uint ENABLE_VIRTUAL_TERMINAL_INPUT = 0x0200;

    // ANSI index -> ConsoleColor mapping (they don't match directly)
    private static readonly ConsoleColor[] AnsiToConsoleColor =
    [
        ConsoleColor.Black,       // ANSI 0
        ConsoleColor.DarkRed,     // ANSI 1
        ConsoleColor.DarkGreen,   // ANSI 2
        ConsoleColor.DarkYellow,  // ANSI 3
        ConsoleColor.DarkBlue,    // ANSI 4
        ConsoleColor.DarkMagenta, // ANSI 5
        ConsoleColor.DarkCyan,    // ANSI 6
        ConsoleColor.Gray,        // ANSI 7
        ConsoleColor.DarkGray,    // ANSI 8
        ConsoleColor.Red,         // ANSI 9
        ConsoleColor.Green,       // ANSI 10
        ConsoleColor.Yellow,      // ANSI 11
        ConsoleColor.Blue,        // ANSI 12
        ConsoleColor.Magenta,     // ANSI 13
        ConsoleColor.Cyan,        // ANSI 14
        ConsoleColor.White,       // ANSI 15
    ];

    /// <summary>
    /// Queries the terminal for all 16 ANSI color RGB values.
    /// </summary>
    /// <param name="timeoutMs">Timeout in milliseconds for each color query.</param>
    /// <returns>Dictionary mapping ConsoleColor to RGB values, or null if query failed for that color.</returns>
    public static Dictionary<ConsoleColor, (byte R, byte G, byte B)?> QueryAll(int timeoutMs = 100)
    {
        var results = new Dictionary<ConsoleColor, (byte R, byte G, byte B)?>();

        if (Console.IsInputRedirected)
        {
            for (var ansiIndex = 0; ansiIndex < 16; ansiIndex++)
            {
                results[AnsiToConsoleColor[ansiIndex]] = null;
            }
            return results;
        }

        var inputHandle = GetStdHandle(STD_INPUT_HANDLE);
        var gotMode = GetConsoleMode(inputHandle, out var originalMode);

        try
        {
            SetConsoleMode(inputHandle, originalMode | ENABLE_VIRTUAL_TERMINAL_INPUT);

            for (var ansiIndex = 0; ansiIndex < 16; ansiIndex++)
            {
                var rgb = QueryColor(ansiIndex, timeoutMs);
                var consoleColor = AnsiToConsoleColor[ansiIndex];
                results[consoleColor] = rgb;
            }
        }
        finally
        {
            if (gotMode)
            {
                SetConsoleMode(inputHandle, originalMode);
            }
        }

        return results;
    }

    /// <summary>
    /// Queries the terminal for a specific ConsoleColor's RGB value.
    /// </summary>
    public static (byte R, byte G, byte B)? Query(ConsoleColor color, int timeoutMs = 100)
    {
        var ansiIndex = Array.IndexOf(AnsiToConsoleColor, color);
        if (ansiIndex < 0 || Console.IsInputRedirected)
        {
            return null;
        }

        var inputHandle = GetStdHandle(STD_INPUT_HANDLE);
        var gotMode = GetConsoleMode(inputHandle, out var originalMode);

        try
        {
            SetConsoleMode(inputHandle, originalMode | ENABLE_VIRTUAL_TERMINAL_INPUT);
            return QueryColor(ansiIndex, timeoutMs);
        }
        finally
        {
            if (gotMode)
            {
                SetConsoleMode(inputHandle, originalMode);
            }
        }
    }

    /// <summary>
    /// Queries the terminal's default foreground color.
    /// </summary>
    public static (byte R, byte G, byte B)? QueryForeground(int timeoutMs = 100)
    {
        return QueryOscColor(10, timeoutMs);
    }

    /// <summary>
    /// Queries the terminal's default background color.
    /// </summary>
    public static (byte R, byte G, byte B)? QueryBackground(int timeoutMs = 100)
    {
        return QueryOscColor(11, timeoutMs);
    }

    /// <summary>
    /// Queries the terminal's cursor color.
    /// </summary>
    public static (byte R, byte G, byte B)? QueryCursor(int timeoutMs = 100)
    {
        return QueryOscColor(12, timeoutMs);
    }

    private static (byte R, byte G, byte B)? QueryOscColor(int oscCode, int timeoutMs)
    {
        if (Console.IsInputRedirected)
        {
            return null;
        }

        var inputHandle = GetStdHandle(STD_INPUT_HANDLE);
        var gotMode = GetConsoleMode(inputHandle, out var originalMode);

        try
        {
            SetConsoleMode(inputHandle, originalMode | ENABLE_VIRTUAL_TERMINAL_INPUT);

            Console.Out.Flush();
            Console.Write($"\e]{oscCode};?\e\\");
            Console.Out.Flush();

            var response = ReadResponse(timeoutMs);
            return ParseColorResponse(response);
        }
        finally
        {
            if (gotMode)
            {
                SetConsoleMode(inputHandle, originalMode);
            }
        }
    }

    private static (byte R, byte G, byte B)? QueryColor(int ansiIndex, int timeoutMs)
    {
        Console.Out.Flush();
        Console.Write($"\e]4;{ansiIndex};?\e\\");
        Console.Out.Flush();

        var response = ReadResponse(timeoutMs);
        return ParseColorResponse(response);
    }

    private static (byte R, byte G, byte B)? ParseColorResponse(string? response)
    {
        if (string.IsNullOrEmpty(response))
        {
            return null;
        }

        var idx = response.IndexOf("rgb:", StringComparison.Ordinal);
        if (idx < 0 || idx + 18 > response.Length)
        {
            return null;
        }

        var span = response.AsSpan(idx + 4); // skip "rgb:"

        // Format: RRRR/GGGG/BBBB (positions 0-3, 5-8, 10-13)
        if (span[4] != '/' || span[9] != '/')
        {
            return null;
        }

        if (!int.TryParse(span[..4], NumberStyles.HexNumber, null, out var r) ||
            !int.TryParse(span.Slice(5, 4), NumberStyles.HexNumber, null, out var g) ||
            !int.TryParse(span.Slice(10, 4), NumberStyles.HexNumber, null, out var b))
        {
            return null;
        }

        // Convert 16-bit hex values to 8-bit (take high byte)
        return ((byte)(r >> 8), (byte)(g >> 8), (byte)(b >> 8));
    }

    private static string ReadResponse(int timeoutMs)
    {
        var sb = new StringBuilder();
        var sw = Stopwatch.StartNew();

        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (!Console.KeyAvailable)
            {
                continue;
            }

            var c = Console.ReadKey(true).KeyChar;
            if (c == 0)
            {
                continue;
            }

            sb.Append(c);

            var current = sb.ToString();
            if ((current.Length >= 2 && current[^2] == '\x1b' && current[^1] == '\\') ||
                (current.Length >= 1 && current[^1] == '\x07'))
            {
                return current;
            }
        }

        return sb.ToString();
    }
}