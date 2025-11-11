using Spectre.Console.Cli;

namespace VcrSharp.Cli.Commands;

/// <summary>
/// Deconstructs Key=Value pairs for --set command-line option.
/// </summary>
public sealed class SetCommandDeconstructor : PairDeconstructor<string, string>
{
    /// <summary>
    /// Parses a Key=Value string into its components.
    /// </summary>
    /// <param name="value">The Key=Value string to parse (e.g., "FontSize=24", "Theme=Dracula")</param>
    /// <returns>A tuple containing the setting name and value</returns>
    /// <exception cref="ArgumentException">Thrown when the value is not in Key=Value format</exception>
    protected override (string, string) Deconstruct(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("SET parameter cannot be empty", nameof(value));
        }

        var parts = value.Split('=', 2);
        if (parts.Length != 2)
        {
            throw new ArgumentException(
                $"Invalid SET parameter format: '{value}'. Expected format: Key=Value (e.g., FontSize=24, Theme=Dracula)",
                nameof(value));
        }

        var key = parts[0].Trim();
        var val = parts[1].Trim();

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException($"SET parameter key cannot be empty in: '{value}'", nameof(value));
        }

        if (string.IsNullOrWhiteSpace(val))
        {
            throw new ArgumentException($"SET parameter value cannot be empty in: '{value}'", nameof(value));
        }

        return (key, val);
    }
}
