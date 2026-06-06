using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using VcrSharp.Core.Parsing;
using VcrSharp.Core.Parsing.Ast;
using VcrSharp.Core.Session;

namespace VcrSharp.Core.Config;

/// <summary>Options controlling a migration run.</summary>
public sealed class MigrateOptions
{
    /// <summary>Name of the primary (largest cluster) preset. Default "doc".</summary>
    public string PresetName { get; init; } = "doc";

    /// <summary>
    /// Fraction of a cluster's tapes that must share a (setting,value) pair for it to move into the
    /// preset. Default 0.6 (a strong majority).
    /// </summary>
    public double Threshold { get; init; } = 0.6;

    /// <summary>Maximum number of distinct profiles (presets) to mine. Default 6.</summary>
    public int MaxClusters { get; init; } = 6;
}

/// <summary>The rewrite computed for a single tape.</summary>
public sealed class TapeRewrite
{
    public required string Path { get; init; }
    public required string OriginalText { get; init; }
    public string NewText { get; set; } = string.Empty;
    public string? PresetName { get; set; }
    public List<string> RemovedLines { get; } = new();
    public List<string> AddedLines { get; } = new();
    public bool Changed { get; set; }

    /// <summary>True when the rewritten tape resolves to the same effective config + actions.</summary>
    public bool EquivalenceOk { get; set; }

    /// <summary>Set when the tape was left untouched (parse failure, no change, or equivalence drift).</summary>
    public string? SkipReason { get; set; }
}

/// <summary>A discovered profile: a shared house style and the tapes that fit it cleanly.</summary>
public sealed class TapeCluster
{
    public string Name { get; set; } = "";
    public required Dictionary<string, string> House { get; init; }
    public List<string> Paths { get; } = new();
}

/// <summary>The full plan: the generated config plus a rewrite per tape.</summary>
public sealed class MigrationPlan
{
    public required string ConfigPath { get; init; }
    public required string ConfigToml { get; init; }
    public List<TapeCluster> Clusters { get; } = new();
    public List<TapeRewrite> Rewrites { get; } = new();

    public int DuplicatedLinesRemoved => Rewrites.Where(r => r.EquivalenceOk).Sum(r => r.RemovedLines.Count);
    public int Rewritten => Rewrites.Count(r => r is { Changed: true, EquivalenceOk: true });
    public int Skipped => Rewrites.Count(r => r.SkipReason is not null);
}

/// <summary>
/// Migrates a directory of legacy tapes to the config-layer model. It clusters tapes into profiles
/// (by equivalence-fit, so a tape only joins a profile if applying that preset leaves its realized
/// config byte-identical), mines a shared <c>base</c> plus one child preset per profile, and rewrites
/// each tape to <c>Use</c> its profile plus only its per-tape overrides. Any tape that fits no profile,
/// or whose rewrite would drift, is left untouched.
/// </summary>
public static partial class TapeMigrator
{
    [GeneratedRegex(@"^-?\d+(\.\d+)?(ms|m|s)$", RegexOptions.CultureInvariant)]
    private static partial Regex DurationShape();

    private const string BasePresetName = "base";

    /// <summary>Computes the migration plan for a set of (path, text) tapes sharing one config directory.</summary>
    public static MigrationPlan Plan(IReadOnlyList<(string Path, string Text)> tapes, string configDir, MigrateOptions options)
    {
        var parser = new TapeParser();

        var parsed = new List<(string Path, string Text, List<ICommand>? Commands)>();
        foreach (var (path, text) in tapes)
        {
            List<ICommand>? commands = null;
            try { commands = parser.ParseTape(text, path); }
            catch { /* unparseable -> leave untouched */ }
            parsed.Add((path, text, commands));
        }

        var usable = parsed.Where(p => p.Commands is not null)
            .Select(p => (p.Path, Commands: p.Commands!))
            .ToList();

        // 1. Cluster tapes into profiles by equivalence-fit, then name them.
        var clusters = ClusterByFit(usable, options);
        NameClusters(clusters, options);

        // 2. Mine the shared base (pairs common to every profile) and render vcr.toml.
        var basePairs = clusters.Count > 1 ? CommonPairs(clusters) : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var configToml = RenderConfigToml(clusters, basePairs);
        var configPath = Path.Combine(configDir, "vcr.toml");
        var config = VcrConfigReader.Parse(configToml, configPath);

        var plan = new MigrationPlan { ConfigPath = configPath, ConfigToml = configToml };
        plan.Clusters.AddRange(clusters);

        var clusterOf = new Dictionary<string, TapeCluster>(StringComparer.Ordinal);
        foreach (var cluster in clusters)
            foreach (var path in cluster.Paths)
                clusterOf[path] = cluster;

        foreach (var (path, text, commands) in parsed)
        {
            var rewrite = new TapeRewrite { Path = path, OriginalText = text, NewText = text };

            if (commands is null)
            {
                rewrite.SkipReason = "could not parse";
                plan.Rewrites.Add(rewrite);
                continue;
            }

            if (!clusterOf.TryGetValue(path, out var cluster))
            {
                rewrite.SkipReason = "no shared profile";
                plan.Rewrites.Add(rewrite);
                continue;
            }

            rewrite.PresetName = cluster.Name;
            RewriteText(rewrite, cluster.House, cluster.Name);

            if (!rewrite.Changed)
            {
                rewrite.SkipReason = "nothing to extract";
                plan.Rewrites.Add(rewrite);
                continue;
            }

            // 3. Equivalence gate: original vs rewritten must realize the same config + actions.
            var before = Fingerprint(PresetResolver.Resolve(commands, path, config: null));
            string after;
            try
            {
                var newCommands = parser.ParseTape(rewrite.NewText, path);
                after = Fingerprint(PresetResolver.Resolve(newCommands, path, config));
            }
            catch (Exception ex)
            {
                rewrite.SkipReason = $"rewrite did not re-parse ({ex.Message})";
                rewrite.NewText = text;
                plan.Rewrites.Add(rewrite);
                continue;
            }

            rewrite.EquivalenceOk = before == after;
            if (!rewrite.EquivalenceOk)
            {
                rewrite.SkipReason = "equivalence drift (left untouched)";
                rewrite.NewText = text;
            }

            plan.Rewrites.Add(rewrite);
        }

        return plan;
    }

    /// <summary>
    /// Greedily peels off profiles: mine the majority house style of the remaining tapes, claim every
    /// remaining tape that applies cleanly (equivalence-preserving) to it, and repeat on the rest.
    /// </summary>
    private static List<TapeCluster> ClusterByFit(List<(string Path, List<ICommand> Commands)> usable, MigrateOptions options)
    {
        var clusters = new List<TapeCluster>();
        var remaining = usable.ToList();

        while (remaining.Count >= 2 && clusters.Count < options.MaxClusters)
        {
            var house = MineHouseStyle(remaining.Select(r => r.Commands).ToList(), options.Threshold);
            if (house.Count == 0)
                break;

            var fit = remaining.Where(t => FitsCleanly(t.Commands, t.Path, house)).ToList();
            if (fit.Count < 2)
                break;

            var cluster = new TapeCluster { House = house };
            cluster.Paths.AddRange(fit.Select(f => f.Path));
            clusters.Add(cluster);

            var fitSet = fit.Select(f => f.Path).ToHashSet(StringComparer.Ordinal);
            remaining = remaining.Where(t => !fitSet.Contains(t.Path)).ToList();
        }

        return clusters;
    }

    /// <summary>True if dropping the house-matching Sets and pulling the preset leaves the tape equivalent.</summary>
    private static bool FitsCleanly(List<ICommand> commands, string path, Dictionary<string, string> house)
    {
        var (newCommands, removed) = ApplyHouseAtCommandLevel(commands, house, "_probe");
        if (removed == 0)
            return false;

        var probe = ConfigFromHouse("_probe", house);
        var before = Fingerprint(PresetResolver.Resolve(commands, path, config: null));
        var after = Fingerprint(PresetResolver.Resolve(newCommands, path, probe));
        return before == after;
    }

    private static (List<ICommand> Commands, int Removed) ApplyHouseAtCommandLevel(
        List<ICommand> commands, Dictionary<string, string> house, string presetName)
    {
        var result = new List<ICommand> { new UseCommand(presetName) };
        var removed = 0;
        foreach (var c in commands)
        {
            if (c is SetCommand s && house.TryGetValue(s.SettingName, out var hv)
                && CanonicalEquals(s.Value.ToString() ?? "", hv))
            {
                removed++;
                continue;
            }
            result.Add(c);
        }

        return (result, removed);
    }

    private static VcrConfig ConfigFromHouse(string name, Dictionary<string, string> house)
    {
        var config = new VcrConfig();
        var preset = new VcrPreset { Name = name };
        foreach (var (key, value) in house)
            preset.Settings[key] = value; // raw string; ApplySetting parses it
        config.Presets[name] = preset;
        return config;
    }

    /// <summary>Pairs (key, value) present with the same canonical value in every cluster's house.</summary>
    private static Dictionary<string, string> CommonPairs(List<TapeCluster> clusters)
    {
        var common = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var first = clusters[0].House;
        foreach (var (key, value) in first)
        {
            if (clusters.All(c => c.House.TryGetValue(key, out var v) && CanonicalEquals(v, value)))
                common[key] = value;
        }

        return common;
    }

    private static void NameClusters(List<TapeCluster> clusters, MigrateOptions options)
    {
        // Largest cluster first so the primary profile is the most common one.
        clusters.Sort((a, b) => b.Paths.Count.CompareTo(a.Paths.Count));

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { BasePresetName };
        for (var i = 0; i < clusters.Count; i++)
        {
            var name = i == 0
                ? options.PresetName
                : MajorityFilenameToken(clusters[i].Paths) ?? $"profile{i + 1}";

            // Disambiguate collisions (e.g. two landing-ish profiles -> landing, landing2).
            var baseName = name;
            var k = 2;
            while (used.Contains(name))
                name = baseName + k++;

            used.Add(name);
            clusters[i].Name = name;
        }
    }

    /// <summary>The leading filename token shared by a majority of a cluster's tapes (e.g. "landing"), or null.</summary>
    private static string? MajorityFilenameToken(List<string> paths)
    {
        var tokens = paths
            .Select(p =>
            {
                var name = Path.GetFileNameWithoutExtension(p) ?? "";
                var cut = name.IndexOfAny(new[] { '-', '_' });
                var token = cut > 0 ? name[..cut] : name;
                return new string(token.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
            })
            .Where(t => t.Length >= 2)
            .ToList();

        if (tokens.Count == 0)
            return null;

        var best = tokens.GroupBy(t => t).OrderByDescending(g => g.Count()).First();
        return best.Count() * 2 >= paths.Count ? best.Key : null; // shared by >= half the cluster
    }

    /// <summary>Per setting key, the most common value, if shared by at least <paramref name="threshold"/> of tapes.</summary>
    private static Dictionary<string, string> MineHouseStyle(List<List<ICommand>> tapes, double threshold)
    {
        var n = tapes.Count;
        if (n == 0) return new(StringComparer.OrdinalIgnoreCase);

        var tally = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var commands in tapes)
        {
            foreach (var (key, value) in EffectiveSetMap(commands))
            {
                if (!tally.TryGetValue(key, out var counts))
                    tally[key] = counts = new Dictionary<string, int>();
                counts[value] = counts.GetValueOrDefault(value) + 1;
            }
        }

        var minCount = Math.Max(2, (int)Math.Ceiling(n * threshold));
        var house = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, counts) in tally)
        {
            var (value, count) = counts.OrderByDescending(c => c.Value).First();
            if (count >= minCount)
                house[key] = value;
        }

        return house;
    }

    /// <summary>The effective Set map for a command list (last write wins), as raw string values.</summary>
    private static Dictionary<string, string> EffectiveSetMap(IEnumerable<ICommand> commands)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var set in commands.OfType<SetCommand>())
            map[set.SettingName] = set.Value.ToString() ?? string.Empty;
        return map;
    }

    private static void RewriteText(TapeRewrite rewrite, Dictionary<string, string> house, string presetName)
    {
        var newline = rewrite.OriginalText.Contains("\r\n") ? "\r\n" : "\n";
        var lines = rewrite.OriginalText.Replace("\r\n", "\n").Split('\n').ToList();

        var kept = new List<string>();
        var useInserted = false;
        var insertedAfterOutput = false;

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            if (trimmed.StartsWith("Set ", StringComparison.OrdinalIgnoreCase) && TryParseSetLine(trimmed, out var key, out var value)
                && house.TryGetValue(key, out var houseValue) && CanonicalEquals(value, houseValue))
            {
                rewrite.RemovedLines.Add(trimmed);
                continue;
            }

            kept.Add(line);

            if (!useInserted)
            {
                if (trimmed.StartsWith("Output ", StringComparison.OrdinalIgnoreCase))
                {
                    insertedAfterOutput = true;
                }
                else if (insertedAfterOutput || (trimmed.Length > 0 && !trimmed.StartsWith('#')))
                {
                    var insertAt = insertedAfterOutput ? kept.Count : kept.Count - 1;
                    kept.Insert(insertAt, $"Use {presetName}");
                    rewrite.AddedLines.Add($"Use {presetName}");
                    useInserted = true;
                }
            }
        }

        if (!useInserted && rewrite.RemovedLines.Count > 0)
        {
            kept.Insert(0, $"Use {presetName}");
            rewrite.AddedLines.Add($"Use {presetName}");
            useInserted = true;
        }

        rewrite.Changed = rewrite.RemovedLines.Count > 0 && useInserted;
        rewrite.NewText = string.Join(newline, kept);
    }

    private static bool TryParseSetLine(string trimmed, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;

        var rest = trimmed["Set".Length..].Trim();
        var sp = rest.IndexOf(' ');
        if (sp <= 0) return false;

        key = rest[..sp];
        value = rest[(sp + 1)..].Trim();

        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') ||
             (value[0] == '\'' && value[^1] == '\'') ||
             (value[0] == '`' && value[^1] == '`')))
        {
            value = value[1..^1];
        }

        return key.Length > 0;
    }

    /// <summary>Compares two raw setting values, tolerating bool/duration spelling differences.</summary>
    private static bool CanonicalEquals(string a, string b)
        => Canonicalize(a).Equals(Canonicalize(b), StringComparison.OrdinalIgnoreCase);

    private static string Canonicalize(object value)
    {
        switch (value)
        {
            case bool b:
                return b ? "true" : "false";
            case TimeSpan ts:
                return ts.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
        }

        var s = (value.ToString() ?? string.Empty).Trim();
        if (s.Equals("true", StringComparison.OrdinalIgnoreCase)) return "true";
        if (s.Equals("false", StringComparison.OrdinalIgnoreCase)) return "false";

        if (s.Contains(':') && TimeSpan.TryParse(s, CultureInfo.InvariantCulture, out var parsed))
            return parsed.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
        if (DurationShape().IsMatch(s))
            return ParseDuration(s).TotalMilliseconds.ToString(CultureInfo.InvariantCulture);

        return s;
    }

    private static TimeSpan ParseDuration(string value)
    {
        if (value.EndsWith("ms", StringComparison.Ordinal))
            return TimeSpan.FromMilliseconds(double.Parse(value[..^2], CultureInfo.InvariantCulture));
        if (value.EndsWith("m", StringComparison.Ordinal))
            return TimeSpan.FromMinutes(double.Parse(value[..^1], CultureInfo.InvariantCulture));
        return TimeSpan.FromSeconds(double.Parse(value[..^1], CultureInfo.InvariantCulture));
    }

    /// <summary>A stable fingerprint of the realized config + Output + action sequence for equivalence checking.</summary>
    private static string Fingerprint(List<ICommand> commands)
    {
        var options = SessionOptions.FromCommands(commands);
        var sb = new StringBuilder();

        sb.Append("cols=").Append(options.Cols).Append(';');
        sb.Append("rows=").Append(options.Rows).Append(';');
        sb.Append("w=").Append(options.Width).Append(';');
        sb.Append("h=").Append(options.Height).Append(';');
        sb.Append("font=").Append(options.FontSize).Append('/').Append(options.FontFamily).Append(';');
        sb.Append("ls=").Append(options.LetterSpacing).Append(";lh=").Append(options.LineHeight).Append(';');
        sb.Append("theme=").Append(options.Theme.Name).Append(';');
        sb.Append("transparent=").Append(options.TransparentBackground).Append(';');
        sb.Append("cursor=").Append(options.DisableCursor).Append('/').Append(options.CursorBlink).Append(';');
        sb.Append("pad=").Append(options.Padding).Append(";margin=").Append(options.Margin).Append(';');
        sb.Append("endbuf=").Append(options.EndBuffer.TotalMilliseconds).Append(';');
        sb.Append("startbuf=").Append(options.StartBuffer.TotalMilliseconds).Append(';');
        sb.Append("typing=").Append(options.TypingSpeed.TotalMilliseconds).Append(';');
        sb.Append("shell=").Append(options.Shell).Append(';');
        sb.Append("cwd=").Append(options.WorkingDirectory).Append(';');
        sb.Append("static=").Append(options.StaticOutput).Append(";fit=").Append(options.FitToContent).Append(';');
        sb.Append("framerate=").Append(options.Framerate).Append(';');
        sb.Append("out=[").Append(string.Join(",", options.OutputFiles)).Append("];");
        sb.Append("env=[").Append(string.Join(",", options.Environment.OrderBy(e => e.Key).Select(e => $"{e.Key}={e.Value}"))).Append("];");

        sb.Append("actions=[");
        foreach (var c in commands)
        {
            if (c is SetCommand or OutputCommand or EnvCommand or UseCommand)
                continue;
            sb.Append(c).Append('|');
        }
        sb.Append(']');

        return sb.ToString();
    }

    private static string RenderConfigToml(List<TapeCluster> clusters, Dictionary<string, string> basePairs)
    {
        var sb = new StringBuilder();
        sb.Append("# Generated by `vcr migrate`. Shared house style for this directory's tapes.\n");
        sb.Append("# Pulled in by `Use <preset>` at the top of each tape.\n\n");

        if (clusters.Count == 0)
            return sb.ToString();

        var useBase = clusters.Count > 1 && basePairs.Count > 0;

        if (useBase)
        {
            sb.Append('[').Append("preset.").Append(BasePresetName).Append("]\n");
            AppendSettings(sb, basePairs);
            sb.Append('\n');
        }

        foreach (var cluster in clusters)
        {
            sb.Append('[').Append("preset.").Append(cluster.Name).Append("]\n");
            if (useBase)
                sb.Append("inherits = \"").Append(BasePresetName).Append("\"\n");

            var pairs = useBase
                ? cluster.House.Where(kv => !basePairs.ContainsKey(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase)
                : cluster.House;
            AppendSettings(sb, pairs);
            sb.Append('\n');
        }

        return sb.ToString().TrimEnd('\n') + "\n";
    }

    private static void AppendSettings(StringBuilder sb, Dictionary<string, string> settings)
    {
        foreach (var (key, value) in settings.OrderBy(k => PreferredOrder(k.Key)).ThenBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            sb.Append(LowerFirst(key)).Append(" = ").Append(RenderTomlValue(value)).Append('\n');
    }

    private static int PreferredOrder(string key) => key.ToLowerInvariant() switch
    {
        "theme" => 0,
        "fontsize" => 1,
        "fontfamily" => 2,
        "cols" => 3,
        "rows" => 4,
        "transparentbackground" => 5,
        "disablecursor" => 6,
        "staticoutput" => 7,
        "endbuffer" => 8,
        "shell" => 9,
        "workingdirectory" => 10,
        _ => 50,
    };

    private static string LowerFirst(string key) => key.Length == 0 ? key : char.ToLowerInvariant(key[0]) + key[1..];

    private static string RenderTomlValue(string tapeValue)
    {
        if (tapeValue.Equals("true", StringComparison.OrdinalIgnoreCase) || tapeValue.Equals("false", StringComparison.OrdinalIgnoreCase))
            return tapeValue.ToLowerInvariant();

        if (tapeValue.Contains(':') && TimeSpan.TryParse(tapeValue, CultureInfo.InvariantCulture, out var ts))
            return RenderDuration(ts);

        if (double.TryParse(tapeValue, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
            return tapeValue;

        return $"\"{tapeValue}\"";
    }

    private static string RenderDuration(TimeSpan ts)
    {
        if (ts.TotalMilliseconds % 1000 != 0)
            return $"{ts.TotalMilliseconds.ToString(CultureInfo.InvariantCulture)}ms";
        return $"{((int)ts.TotalSeconds).ToString(CultureInfo.InvariantCulture)}s";
    }
}
