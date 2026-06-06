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
    /// <summary>
    /// Deprecated <c>Set</c> names → guidance. Only the two still kept for migration compatibility
    /// remain: <c>StaticOutput</c>/<c>FitToContent</c> still parse (so <c>vcr migrate</c> can read
    /// legacy tapes and rewrite them) but are superseded by <c>Mode</c>/<c>Size</c>. The genuinely
    /// dead names (Width/Height/WaitPattern/CssVariables) and commands (Require/Source/Copy/Paste)
    /// have been removed from the grammar outright.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Settings =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["StaticOutput"] = "use 'Set Mode static' (Mode is animated|static)",
            ["FitToContent"] = "use 'Set Size fit' (Size is grid|fit)",
        };

    /// <summary>No commands are deprecated-but-parseable; removed ones are now parse errors.</summary>
    public static readonly IReadOnlyDictionary<string, string> Commands =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
