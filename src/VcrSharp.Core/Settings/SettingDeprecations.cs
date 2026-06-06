using VcrSharp.Core.Parsing.Ast;

namespace VcrSharp.Core.Settings;

/// <summary>
/// The Phase-2 "ruthless prune": settings and commands that are dead, no-ops on the primary SVG
/// path, or superseded. They still parse and apply (nothing breaks), but using them surfaces a
/// deprecation warning pointing at the replacement. This shrinks the felt surface without changing
/// any rendered output — the removal itself happens in a later release.
/// </summary>
public static class SettingDeprecations
{
    /// <summary>Deprecated <c>Set</c> names → guidance shown to the author.</summary>
    public static readonly IReadOnlyDictionary<string, string> Settings =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Superseded sizing
            ["Width"] = "use Cols (Width is ignored when Cols is set)",
            ["Height"] = "use Rows (Height is ignored when Rows is set)",

            // No-ops on the SVG output path
            ["Margin"] = "has no effect on SVG output",
            ["MarginFill"] = "has no effect on SVG output",
            ["WindowBarSize"] = "has no effect on SVG output",
            ["BorderRadius"] = "has no effect on SVG output",

            // Subsumed / unused styling
            ["CursorBlink"] = "use DisableCursor",
            ["LetterSpacing"] = "no longer used",
            ["LineHeight"] = "no longer used",
            ["FontFamily"] = "rarely needed; the default monospace stack is recommended",

            // Looping is meaningless for a static SVG embed
            ["Loop"] = "looping does not apply to static SVG output",
            ["LoopCount"] = "looping does not apply to static SVG output",
            ["LoopOffset"] = "looping does not apply to static SVG output",
            ["PlaybackSpeed"] = "no longer used",
            ["MaxColors"] = "GIF-only and rarely needed",

            // SVG embedding knobs — behavior is now always-on; the toggles are going away
            ["CssVariables"] = "removed from the per-tape surface",
            ["SvgIntrinsicSize"] = "always on now; the toggle is no longer needed",
            ["SvgMetadata"] = "always on now; the toggle is no longer needed",

            // Static/animation
            ["StaticOutput"] = "use 'Set Animate false' (Animate is the inverse of StaticOutput)",

            // The six overlapping timing knobs collapse to EndBuffer/HoldDuration + inline Wait@
            ["WaitTimeout"] = "use an inline modifier on Wait, e.g. 'Wait@30s /pattern/'",
            ["WaitPattern"] = "pass a regex directly to Wait, e.g. 'Wait /pattern/'",
            ["InactivityTimeout"] = "settle timing is now automatic",
            ["MaxWaitForInactivity"] = "settle timing is now automatic",
            ["StartWaitTimeout"] = "settle timing is now automatic",
            ["StartBuffer"] = "settle timing is now automatic",
            ["StartupDelay"] = "moved to an internal default",
            ["ScreenshotWaitForInactivity"] = "Screenshot waits for output to settle automatically",
            ["ScreenshotInactivityTimeout"] = "Screenshot waits for output to settle automatically",
        };

    /// <summary>Deprecated command keywords → guidance shown to the author.</summary>
    public static readonly IReadOnlyDictionary<string, string> Commands =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Require"] = "a clear runtime error already covers the missing-program case",
            ["Source"] = "use 'Use <preset>' with a vcr.toml instead",
            ["Hide"] = "frame gating does not apply to static SVG output",
            ["Show"] = "frame gating does not apply to static SVG output",
            ["Copy"] = "clipboard commands are being removed",
            ["Paste"] = "clipboard commands are being removed",
        };

    /// <summary>
    /// Collects deprecation warnings for the commands a user authored. Pass the parsed tape commands
    /// (before preset resolution) so line numbers point at the tape, not generated preset defaults.
    /// </summary>
    public static List<string> Collect(IEnumerable<ICommand> commands)
    {
        var warnings = new List<string>();

        foreach (var command in commands)
        {
            if (command is SetCommand set && Settings.TryGetValue(set.SettingName, out var settingMessage))
            {
                var where = set.LineNumber > 0 ? $"line {set.LineNumber}: " : "";
                warnings.Add($"{where}'Set {set.SettingName}' is deprecated — {settingMessage}.");
                continue;
            }

            var keyword = command.GetType().Name.Replace("Command", "");
            if (Commands.TryGetValue(keyword, out var commandMessage))
                warnings.Add($"'{keyword}' is deprecated — {commandMessage}.");
        }

        return warnings;
    }
}
