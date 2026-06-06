using System.Text;
using Shouldly;

namespace VcrSharp.Terminal.Tests.Conformance;

/// <summary>
/// The conformance "scoreboard": replays the vendored libvterm corpus against <see cref="VtScreen"/>.
///
/// Two gates plus a report:
///  • <see cref="EngineDoesNotCrash"/> — a hard, always-must-pass robustness gate: the engine must not
///    throw on any real-world sequence in the corpus.
///  • <see cref="Scoreboard"/> — a measurement (always green): it prints and persists a per-file
///    pass/fail/skip table so we can watch conformance climb phase by phase. It is NOT a pass/fail gate
///    yet; the engine is a known ~30% proof-of-concept (see docs/vt-engine-conformance.md). When a phase
///    lands, we ratchet specific files into <see cref="EngineDoesNotCrash"/>-style hard assertions.
/// </summary>
public sealed class LibVtermConformanceTests(ITestOutputHelper output)
{
    [Theory]
    [MemberData(nameof(Corpus.TestFiles), MemberType = typeof(Corpus))]
    public void EngineDoesNotCrash(string file)
    {
        var result = LibVtermHarness.Run(Corpus.PathOf(file));
        result.Errors.ShouldBe(0, $"{file} raised {result.Errors} engine/harness error(s):\n  " +
            string.Join("\n  ", result.Failures));
    }

    [Fact]
    public void Scoreboard()
    {
        var results = Corpus.Files()
            .Select(f => LibVtermHarness.Run(Corpus.PathOf(f)))
            .OrderBy(r => r.File, StringComparer.Ordinal)
            .ToList();

        results.ShouldNotBeEmpty("conformance corpus did not load — check the Content glob in the csproj");

        var report = BuildReport(results);
        output.WriteLine(report);
        Persist(report);

        // Always green: this is a measurement, not a gate (yet). We only assert the harness actually ran.
        results.Sum(r => r.Evaluated).ShouldBeGreaterThan(0, "no assertions were evaluated");
    }

    private static string BuildReport(IReadOnlyList<ConformanceResult> results)
    {
        var totalPass = results.Sum(r => r.Passed);
        var totalFail = results.Sum(r => r.Failed);
        var totalSkip = results.Sum(r => r.Skipped);
        var totalErr = results.Sum(r => r.Errors);
        var evaluated = totalPass + totalFail;
        var overall = evaluated == 0 ? 0 : 100.0 * totalPass / evaluated;

        var sb = new StringBuilder();
        sb.AppendLine("# VcrSharp VT engine — libvterm conformance scoreboard");
        sb.AppendLine();
        sb.AppendLine($"> Corpus: **{results.Count}** vendored libvterm `*.test` files (MIT). " +
                      "Rate = passed / evaluated; *skipped* assertions depend on engine features not yet modelled " +
                      "(callback events, pen/cell colors, line info). See docs/vt-engine-conformance.md §4.");
        sb.AppendLine();
        sb.AppendLine($"## Overall: {totalPass}/{evaluated} evaluated assertions pass " +
                      $"(**{overall:F1}%**) · {totalSkip} skipped · {totalErr} errors");
        sb.AppendLine();
        sb.AppendLine("| File | Pass | Fail | Skip | Err | Rate |");
        sb.AppendLine("|---|---:|---:|---:|---:|---:|");
        foreach (var r in results)
        {
            var rate = r.Evaluated == 0 ? "—" : $"{100.0 * r.PassRate:F0}%";
            sb.AppendLine($"| {r.File} | {r.Passed} | {r.Failed} | {r.Skipped} | {r.Errors} | {rate} |");
        }
        sb.AppendLine($"| **TOTAL** | **{totalPass}** | **{totalFail}** | **{totalSkip}** | **{totalErr}** | **{overall:F1}%** |");

        // Sample failures from every file with fails, to make the remaining gaps concrete.
        var samples = results.Where(r => r.Failed > 0).OrderByDescending(r => r.Failed);
        var any = false;
        foreach (var r in samples)
        {
            if (!any) { sb.AppendLine(); sb.AppendLine("## Sample failures"); any = true; }
            sb.AppendLine();
            sb.AppendLine($"**{r.File}** ({r.Failed} failing)");
            foreach (var f in r.Failures.Take(3)) sb.AppendLine($"- {f}");
        }
        return sb.ToString();
    }

    private void Persist(string report)
    {
        try
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "vt-conformance-scoreboard.md"), report);
            var repoRoot = Corpus.RepoRoot();
            if (repoRoot is not null)
                File.WriteAllText(Path.Combine(repoRoot, "docs", "vt-conformance-scoreboard.md"), report);
        }
        catch (Exception ex)
        {
            output.WriteLine($"(could not persist scoreboard file: {ex.Message})");
        }
    }
}

/// <summary>Locates the vendored corpus copied next to the test assembly, and the repo root.</summary>
internal static class Corpus
{
    private static string Dir => Path.Combine(AppContext.BaseDirectory, "Conformance", "LibVterm");

    public static IEnumerable<string> Files() =>
        Directory.Exists(Dir)
            ? Directory.EnumerateFiles(Dir, "*.test").Select(Path.GetFileName).OfType<string>()
            : Enumerable.Empty<string>();

    public static string PathOf(string file) => Path.Combine(Dir, file);

    public static IEnumerable<object[]> TestFiles() => Files().OrderBy(f => f, StringComparer.Ordinal).Select(f => new object[] { f });

    public static string? RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "VcrSharp.sln"))) return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
