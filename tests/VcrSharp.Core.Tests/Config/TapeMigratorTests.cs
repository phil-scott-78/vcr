using Shouldly;
using VcrSharp.Core.Config;

namespace VcrSharp.Core.Tests.Config;

/// <summary>Tests for <see cref="TapeMigrator"/> house-style extraction and equivalence checking.</summary>
public class TapeMigratorTests
{
    private static (string, string) Tape(string name, string body) => (name, body);

    [Fact]
    public void Plan_ExtractsSharedHouseStyle_AndKeepsPerTapeOverrides()
    {
        var common = "Set Theme \"one dark\"\nSet Cols 82\nSet TransparentBackground true\n";
        var tapes = new[]
        {
            Tape("table.tape", $"Output \"assets/table.svg\"\n{common}Set Rows 14\nExec \"x\""),
            Tape("tree.tape",  $"Output \"assets/tree.svg\"\n{common}Set Rows 12\nExec \"y\""),
            Tape("json.tape",  $"Output \"assets/json.svg\"\n{common}Set Rows 18\nExec \"z\""),
        };

        var plan = TapeMigrator.Plan(tapes, "VCR", new MigrateOptions());

        // Preset captured the three shared settings...
        plan.ConfigToml.ShouldContain("[preset.doc]");
        plan.ConfigToml.ShouldContain("theme = \"one dark\"");
        plan.ConfigToml.ShouldContain("cols = 82");
        plan.ConfigToml.ShouldContain("transparentBackground = true");
        // ...but not the per-tape Rows.
        plan.ConfigToml.ShouldNotContain("rows =");

        plan.Rewritten.ShouldBe(3);
        plan.Skipped.ShouldBe(0);
        plan.Rewrites.ShouldAllBe(r => r.EquivalenceOk);

        var table = plan.Rewrites.Single(r => r.Path == "table.tape");
        table.RemovedLines.Count.ShouldBe(3);                 // the three shared Set lines
        table.NewText.ShouldContain("Use doc");
        table.NewText.ShouldContain("Set Rows 14");           // override preserved
        table.NewText.ShouldNotContain("Set Theme");          // moved into preset
        table.NewText.ShouldContain("Output \"assets/table.svg\"");
    }

    [Fact]
    public void Plan_RareSetting_StaysPerTape_NotInPreset()
    {
        var tapes = new[]
        {
            Tape("a.tape", "Set Cols 80\nExec \"x\""),
            Tape("b.tape", "Set Cols 80\nExec \"y\""),
            Tape("c.tape", "Set Cols 80\nSet FontSize 9\nExec \"z\""), // FontSize only here -> below threshold
        };

        var plan = TapeMigrator.Plan(tapes, "VCR", new MigrateOptions());

        plan.ConfigToml.ShouldContain("cols = 80");
        plan.ConfigToml.ShouldNotContain("fontSize");
        plan.Rewrites.Single(r => r.Path == "c.tape").NewText.ShouldContain("Set FontSize 9");
        plan.Rewrites.ShouldAllBe(r => r.SkipReason == null || r.EquivalenceOk);
    }

    [Fact]
    public void Plan_DivergentValueForSameKey_PicksMajority_LeavesMinorityAsOverride()
    {
        // All four share Theme/Cols; the landing fork only diverges on FontSize.
        var shared = "Set Theme \"one dark\"\nSet Cols 82\n";
        var tapes = new[]
        {
            Tape("a.tape", $"{shared}Set FontSize 22\nExec \"x\""),
            Tape("b.tape", $"{shared}Set FontSize 22\nExec \"y\""),
            Tape("c.tape", $"{shared}Set FontSize 22\nExec \"z\""),
            Tape("landing.tape", $"{shared}Set FontSize 13\nExec \"w\""), // the minority fork
        };

        var plan = TapeMigrator.Plan(tapes, "VCR", new MigrateOptions());

        plan.ConfigToml.ShouldContain("fontSize = 22");
        // The 13 tape drops the shared Theme/Cols, keeps its FontSize override, and stays equivalent.
        var landing = plan.Rewrites.Single(r => r.Path == "landing.tape");
        landing.NewText.ShouldContain("Set FontSize 13");
        landing.NewText.ShouldNotContain("Set Theme");
        landing.EquivalenceOk.ShouldBeTrue();
    }

    [Fact]
    public void Plan_TrueFork_SplitsIntoBasePlusChildPresets()
    {
        // Showcase tapes carry endBuffer; the landing fork drops it and adds staticOutput.
        // A single `doc` preset would inject endBuffer into landing -> the migrator must split.
        var tapes = new[]
        {
            Tape("table.tape",   "Set Theme \"one dark\"\nSet Cols 82\nSet EndBuffer 5s\nSet Rows 14\nExec \"a\""),
            Tape("tree.tape",    "Set Theme \"one dark\"\nSet Cols 82\nSet EndBuffer 5s\nSet Rows 12\nExec \"b\""),
            Tape("panel.tape",   "Set Theme \"one dark\"\nSet Cols 82\nSet EndBuffer 5s\nSet Rows 10\nExec \"c\""),
            Tape("landing-x.tape", "Set Theme \"one dark\"\nSet StaticOutput true\nSet FontSize 13\nExec \"d\""),
            Tape("landing-y.tape", "Set Theme \"one dark\"\nSet StaticOutput true\nSet FontSize 13\nExec \"e\""),
        };

        var plan = TapeMigrator.Plan(tapes, "VCR", new MigrateOptions());

        // Two profiles, named doc (primary) and landing (from the filename prefix), over a shared base.
        plan.Clusters.Select(c => c.Name).ShouldBe(new[] { "doc", "landing" }, ignoreOrder: true);
        plan.ConfigToml.ShouldContain("[preset.base]");
        plan.ConfigToml.ShouldContain("theme = \"one dark\"");   // shared -> base
        plan.ConfigToml.ShouldContain("[preset.doc]");
        plan.ConfigToml.ShouldContain("inherits = \"base\"");
        plan.ConfigToml.ShouldContain("[preset.landing]");
        plan.ConfigToml.ShouldContain("staticOutput = true");

        // Every tape migrates and stays equivalent — including the fork, which never gets endBuffer.
        plan.Rewritten.ShouldBe(5);
        plan.Skipped.ShouldBe(0);
        plan.Rewrites.ShouldAllBe(r => r.EquivalenceOk);

        var landing = plan.Rewrites.Single(r => r.Path == "landing-x.tape");
        landing.PresetName.ShouldBe("landing");
        landing.NewText.ShouldContain("Use landing");
    }
}
