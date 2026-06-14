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
            // Size accepts grid | fit | fit-height (alias fit-rows). Separators are normalized away
            // before comparison (see NormalizeEnumValue), mirroring SessionOptions.ApplySetting, so
            // "fit-height", "fit_height" and "fitheight" all validate.
            ["Size"] = new[] { "grid", "fit", "fit-height", "fit-rows" },
        };

    /// <summary>
    /// Strips separators and case so enum-value comparison matches how <c>SessionOptions.ApplySetting</c>
    /// reads the value (e.g. <c>Set Size "fit-height"</c> ≡ <c>fitheight</c>). Keeps the typo lint from
    /// false-flagging a spelling the engine actually honors.
    /// </summary>
    private static string NormalizeEnumValue(string? value) =>
        (value ?? string.Empty).Replace("-", string.Empty).Replace("_", string.Empty).Trim();

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
                         && !allowed.Any(a => NormalizeEnumValue(a)
                             .Equals(NormalizeEnumValue(set.Value.ToString()), StringComparison.OrdinalIgnoreCase)))
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
