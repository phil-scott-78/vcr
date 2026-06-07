using System.Globalization;
using System.Text.RegularExpressions;

namespace VcrSharp.Core.Config;

/// <summary>
/// Reads the small TOML subset VcrSharp uses for <c>vcr.toml</c>: <c>[preset.NAME]</c> and
/// <c>[macro]</c> sections containing <c>key = value</c> pairs, with <c># comments</c>. Values are
/// strings (<c>"..."</c>/<c>'...'</c>), booleans (<c>true</c>/<c>false</c>), durations
/// (<c>5s</c>/<c>500ms</c>/<c>1.5m</c>), or numbers. This is intentionally not a full TOML parser —
/// no arrays, inline tables, or nested keys — just enough to hold a shared house style.
/// </summary>
public static partial class VcrConfigReader
{
    private const string ConfigFileName = "vcr.toml";

    [GeneratedRegex(@"^-?\d+(\.\d+)?(ms|m|s)$", RegexOptions.CultureInvariant)]
    private static partial Regex DurationShape();

    /// <summary>
    /// Walks up the directory tree from <paramref name="tapeFilePath"/> looking for a
    /// <c>vcr.toml</c>. Returns the parsed config, or null if none is found.
    /// </summary>
    public static VcrConfig? Discover(string tapeFilePath)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(tapeFilePath));
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, ConfigFileName);
            if (File.Exists(candidate))
                return Parse(File.ReadAllText(candidate), candidate);

            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }

    /// <summary>Parses vcr.toml text into a <see cref="VcrConfig"/>.</summary>
    public static VcrConfig Parse(string text, string? sourcePath = null)
    {
        var config = new VcrConfig { SourcePath = sourcePath };
        VcrPreset? currentPreset = null;
        var inMacro = false;
        var lineNo = 0;

        foreach (var rawLine in text.Split('\n'))
        {
            lineNo++;
            var line = StripComment(rawLine).Trim();
            if (line.Length == 0)
                continue;

            if (line[0] == '[')
            {
                if (line[^1] != ']')
                    throw new VcrConfigException($"Unterminated section header: '{line}'", lineNo, sourcePath);

                var header = line[1..^1].Trim();
                if (header.Equals("macro", StringComparison.OrdinalIgnoreCase))
                {
                    inMacro = true;
                    currentPreset = null;
                }
                else if (header.StartsWith("preset.", StringComparison.OrdinalIgnoreCase))
                {
                    var name = Unquote(header["preset.".Length..].Trim());
                    if (name.Length == 0)
                        throw new VcrConfigException("Preset section is missing a name (expected [preset.NAME])", lineNo, sourcePath);

                    if (!config.Presets.TryGetValue(name, out currentPreset))
                    {
                        currentPreset = new VcrPreset { Name = name, LineNumber = lineNo };
                        config.Presets[name] = currentPreset;
                    }

                    inMacro = false;
                }
                else
                {
                    throw new VcrConfigException($"Unknown section '[{header}]' (expected [preset.NAME] or [macro])", lineNo, sourcePath);
                }

                continue;
            }

            var eq = line.IndexOf('=');
            if (eq <= 0)
                throw new VcrConfigException($"Expected 'key = value' but found: '{line}'", lineNo, sourcePath);

            var key = line[..eq].Trim();
            var valueText = line[(eq + 1)..].Trim();
            if (key.Length == 0)
                throw new VcrConfigException($"Missing key before '=' on: '{line}'", lineNo, sourcePath);

            if (inMacro)
            {
                config.Macros[key] = Unquote(valueText);
            }
            else if (currentPreset != null)
            {
                switch (key.ToLowerInvariant())
                {
                    case "inherits":
                        currentPreset.Inherits = Unquote(valueText);
                        break;
                    case "outdir":
                        currentPreset.OutDir = Unquote(valueText);
                        break;
                    case "output":
                        currentPreset.OutputTemplate = Unquote(valueText);
                        break;
                    default:
                        currentPreset.Settings[key] = ParseValue(valueText);
                        break;
                }
            }
            else
            {
                throw new VcrConfigException($"'{key}' appears before any [preset.NAME] or [macro] section", lineNo, sourcePath);
            }
        }

        return config;
    }

    /// <summary>
    /// Parses a value into a typed object understood by <c>SessionOptions.ApplySetting</c>:
    /// quoted → string, true/false → bool, duration shape → TimeSpan, number → numeric string,
    /// bare word → string.
    /// </summary>
    private static object ParseValue(string raw)
    {
        if (raw.Length == 0)
            return string.Empty;

        if (raw[0] is '"' or '\'' or '`')
            return Unquote(raw);

        if (raw.Equals("true", StringComparison.OrdinalIgnoreCase))
            return true;
        if (raw.Equals("false", StringComparison.OrdinalIgnoreCase))
            return false;

        if (DurationShape().IsMatch(raw))
            return ParseDuration(raw);

        // Numbers are kept as their literal string; ApplySetting converts via Convert.ToInt32/Single.
        return raw;
    }

    private static TimeSpan ParseDuration(string value)
    {
        if (value.EndsWith("ms", StringComparison.Ordinal))
            return TimeSpan.FromMilliseconds(double.Parse(value[..^2], CultureInfo.InvariantCulture));
        if (value.EndsWith("m", StringComparison.Ordinal))
            return TimeSpan.FromMinutes(double.Parse(value[..^1], CultureInfo.InvariantCulture));
        // "s"
        return TimeSpan.FromSeconds(double.Parse(value[..^1], CultureInfo.InvariantCulture));
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') ||
             (value[0] == '\'' && value[^1] == '\'') ||
             (value[0] == '`' && value[^1] == '`')))
        {
            return value[1..^1];
        }

        return value;
    }

    /// <summary>Removes an unquoted <c>#</c> comment, preserving <c>#</c> inside quoted strings.</summary>
    private static string StripComment(string line)
    {
        var quote = '\0';
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quote != '\0')
            {
                if (c == quote)
                    quote = '\0';
            }
            else if (c is '"' or '\'' or '`')
            {
                quote = c;
            }
            else if (c == '#')
            {
                return line[..i];
            }
        }

        return line;
    }
}
