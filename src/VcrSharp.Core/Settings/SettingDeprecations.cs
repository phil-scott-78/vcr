using VcrSharp.Core.Parsing.Ast;

namespace VcrSharp.Core.Settings;

/// <summary>
/// A deliberately conservative deprecation list: only settings/commands that are genuinely
/// redundant or superseded in every mode — animated, static, and raster (GIF/MP4) alike. Animation
/// is a first-class concern, so the loop/playback/palette/cursor/margin knobs are NOT deprecated.
/// Deprecated names still parse and apply (nothing breaks); using one surfaces a warning pointing at
/// the replacement. Also lints the new <c>Mode</c>/<c>Size</c> values for typos.
/// </summary>
public static class SettingDeprecations
{
    /// <summary>Deprecated <c>Set</c> names → guidance shown to the author.</summary>
    public static readonly IReadOnlyDictionary<string, string> Settings =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Superseded by the terminal-grid sizing model
            ["Width"] = "use Cols (Width is only a fallback when Cols is unset)",
            ["Height"] = "use Rows (Height is only a fallback when Rows is unset)",

            // Renamed to the clear capture/sizing front-ends
            ["StaticOutput"] = "use 'Set Mode static' (Mode is animated|static)",
            ["FitToContent"] = "use 'Set Size fit' (Size is grid|fit)",

            // Redundant with the Wait command itself
            ["WaitPattern"] = "pass a regex directly to Wait, e.g. 'Wait /pattern/'",

            // Speculative theming knob, effectively unused
            ["CssVariables"] = "removed from the per-tape surface",
        };

    /// <summary>Deprecated command keywords → guidance shown to the author.</summary>
    public static readonly IReadOnlyDictionary<string, string> Commands =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Require"] = "a clear runtime error already covers the missing-program case",
            ["Source"] = "use 'Use <preset>' with a vcr.toml instead",
            ["Copy"] = "clipboard commands are being removed",
            ["Paste"] = "clipboard commands are being removed",
        };

    private static readonly IReadOnlyDictionary<string, string[]> EnumSettings =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Mode"] = new[] { "animated", "static" },
            ["Size"] = new[] { "grid", "fit" },
        };

    /// <summary>
    /// Collects deprecation warnings (and Mode/Size typo warnings) for the commands a user authored.
    /// Pass the parsed tape commands (before preset resolution) so line numbers point at the tape.
    /// </summary>
    public static List<string> Collect(IEnumerable<ICommand> commands)
    {
        var warnings = new List<string>();

        foreach (var command in commands)
        {
            if (command is SetCommand set)
            {
                var where = set.LineNumber > 0 ? $"line {set.LineNumber}: " : "";

                if (Settings.TryGetValue(set.SettingName, out var settingMessage))
                {
                    warnings.Add($"{where}'Set {set.SettingName}' is deprecated — {settingMessage}.");
                }
                else if (EnumSettings.TryGetValue(set.SettingName, out var allowed)
                         && !allowed.Contains(set.Value.ToString(), StringComparer.OrdinalIgnoreCase))
                {
                    warnings.Add($"{where}'Set {set.SettingName} {set.Value}' is not recognized — use {string.Join(" or ", allowed)}.");
                }

                continue;
            }

            var keyword = command.GetType().Name.Replace("Command", "");
            if (Commands.TryGetValue(keyword, out var commandMessage))
                warnings.Add($"'{keyword}' is deprecated — {commandMessage}.");
        }

        return warnings;
    }
}
