using VcrSharp.Core.Parsing;
using VcrSharp.Core.Parsing.Ast;

namespace VcrSharp.Core.Config;

/// <summary>
/// Expands the config-layer sugar in a parsed tape against a discovered <c>vcr.toml</c>:
/// <list type="bullet">
///   <item><c>Use NAME</c> → the preset's settings emitted as <see cref="SetCommand"/> defaults
///   (resolving <c>inherits</c> chains; the tape's own <c>Set</c> wins on conflict).</item>
///   <item><c>Exec NAME arg</c> (macro form) → a literal <see cref="ExecCommand"/>.</item>
///   <item><c>Run "cmd"</c> → <see cref="TypeCommand"/> + <see cref="KeyCommand"/> (Enter) +
///   <see cref="WaitCommand"/> (settle).</item>
///   <item>A derived <see cref="OutputCommand"/> from the tape filename when a preset declares an
///   output location and the tape has no explicit <c>Output</c>.</item>
/// </list>
/// The result contains only the primitive command types the recording engine already understands,
/// so Infrastructure needs no changes. With no <c>Use</c>/macro/<c>Run</c> and no config, the input
/// is returned essentially unchanged.
/// </summary>
public static class PresetResolver
{
    /// <summary>Resolves against a vcr.toml discovered by walking up from the tape's directory.</summary>
    public static List<ICommand> ResolveWithDiscovery(List<ICommand> commands, string? tapeFilePath)
    {
        var config = tapeFilePath is null ? null : VcrConfigReader.Discover(tapeFilePath);
        return Resolve(commands, tapeFilePath, config);
    }

    /// <summary>Resolves <paramref name="commands"/> against an explicit (possibly null) config.</summary>
    public static List<ICommand> Resolve(List<ICommand> commands, string? tapeFilePath, VcrConfig? config)
    {
        var uses = commands.OfType<UseCommand>().ToList();
        var hasMacro = commands.OfType<ExecCommand>().Any(e => e.IsMacro);
        var hasRun = commands.OfType<RunCommand>().Any();

        // Fast path: nothing to expand.
        if (uses.Count == 0 && !hasMacro && !hasRun)
            return commands;

        var basename = tapeFilePath is null ? null : Path.GetFileNameWithoutExtension(tapeFilePath);

        // 1. Merge all referenced presets (base-first, later Use wins, then tape Set wins).
        var presetSettings = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        string? outDir = null;
        string? outputTemplate = null;

        foreach (var use in uses)
        {
            if (config is null)
                throw new VcrConfigException(
                    $"'Use {use.PresetName}' but no vcr.toml was found above this tape.", use.LineNumber);

            foreach (var preset in ResolveChain(config, use.PresetName, use.LineNumber))
            {
                foreach (var (key, value) in preset.Settings)
                {
                    if (!TapeParser.IsKnownSetting(key))
                        throw new VcrConfigException(
                            $"Preset '{preset.Name}' sets unknown setting '{key}'.", preset.LineNumber, config.SourcePath);
                    presetSettings[key] = value;
                }

                if (preset.OutDir is not null) { outDir = preset.OutDir; outputTemplate = null; }
                if (preset.OutputTemplate is not null) { outputTemplate = preset.OutputTemplate; outDir = null; }
            }
        }

        // 2. Tape's own Set wins: drop preset entries the tape overrides.
        var tapeSetKeys = commands.OfType<SetCommand>()
            .Select(s => s.SettingName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var hasExplicitOutput = commands.OfType<OutputCommand>().Any();

        // 3. Build the resolved list: preset defaults, then derived Output, then the tape body.
        var result = new List<ICommand>();

        foreach (var key in presetSettings.Keys.Where(k => !tapeSetKeys.Contains(k)).OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            result.Add(new SetCommand(key, presetSettings[key]));

        if (!hasExplicitOutput && basename is not null)
        {
            var derived = DeriveOutputPath(outputTemplate, outDir, basename);
            if (derived is not null)
                result.Add(new OutputCommand(derived));
        }

        foreach (var command in commands)
        {
            switch (command)
            {
                case UseCommand:
                    break; // consumed
                case ExecCommand { IsMacro: true } macro:
                    result.Add(new ExecCommand(ExpandMacro(config, macro, basename), macro.LineNumber));
                    break;
                case RunCommand run:
                    result.Add(new TypeCommand(run.Text));
                    result.Add(new KeyCommand("Enter"));
                    result.Add(new WaitCommand(WaitScope.Buffer));
                    break;
                default:
                    result.Add(command);
                    break;
            }
        }

        return result;
    }

    /// <summary>Returns the inheritance chain for <paramref name="presetName"/>, base-first.</summary>
    private static List<VcrPreset> ResolveChain(VcrConfig config, string presetName, int useLine)
    {
        var chain = new List<VcrPreset>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var name = presetName;

        while (name is not null)
        {
            if (!config.Presets.TryGetValue(name, out var preset))
                throw new VcrConfigException(
                    $"'Use {presetName}' references unknown preset '{name}'.", useLine, config.SourcePath);

            if (!seen.Add(preset.Name))
                throw new VcrConfigException(
                    $"Preset inheritance cycle detected at '{preset.Name}'.", preset.LineNumber, config.SourcePath);

            chain.Add(preset);
            name = preset.Inherits;
        }

        chain.Reverse();
        return chain;
    }

    private static string ExpandMacro(VcrConfig? config, ExecCommand macro, string? basename)
    {
        if (config is null || !config.Macros.TryGetValue(macro.Command, out var template))
            throw new VcrConfigException(
                $"'Exec {macro.Command}' references unknown macro '{macro.Command}'.", macro.LineNumber, config?.SourcePath);

        var arg = macro.MacroArg ?? basename ?? string.Empty;
        return template
            .Replace("{0}", arg)
            .Replace("{name}", basename ?? string.Empty);
    }

    private static string? DeriveOutputPath(string? outputTemplate, string? outDir, string basename)
    {
        if (outputTemplate is not null)
            return outputTemplate.Replace("{name}", basename);

        if (outDir is not null)
            return $"{outDir.TrimEnd('/', '\\')}/{basename}.svg";

        return null;
    }
}
