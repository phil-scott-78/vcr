using Shouldly;
using VcrSharp.Core.Config;

namespace VcrSharp.Core.Tests.Config;

/// <summary>Tests for the vcr.toml subset reader.</summary>
public class VcrConfigReaderTests
{
    [Fact]
    public void Parse_PresetWithTypedValues_ParsesStringBoolDurationNumber()
    {
        var toml = """
            [preset.doc]
            theme = "one dark"
            cols = 82
            transparentBackground = true
            disableCursor = false
            endBuffer = 5s
            typingSpeed = 250ms
            """;

        var config = VcrConfigReader.Parse(toml);

        config.Presets.ShouldContainKey("doc");
        var doc = config.Presets["doc"];
        doc.Settings["theme"].ShouldBe("one dark");
        doc.Settings["cols"].ShouldBe("82");
        doc.Settings["transparentBackground"].ShouldBe(true);
        doc.Settings["disableCursor"].ShouldBe(false);
        doc.Settings["endBuffer"].ShouldBe(TimeSpan.FromSeconds(5));
        doc.Settings["typingSpeed"].ShouldBe(TimeSpan.FromMilliseconds(250));
    }

    [Fact]
    public void Parse_ReservedKeys_AreNotTreatedAsSettings()
    {
        var toml = """
            [preset.landing]
            inherits = "doc"
            outDir = "Spectre.Docs/Content/assets"
            fontSize = 13
            """;

        var landing = VcrConfigReader.Parse(toml).Presets["landing"];
        landing.Inherits.ShouldBe("doc");
        landing.OutDir.ShouldBe("Spectre.Docs/Content/assets");
        landing.Settings.ShouldNotContainKey("inherits");
        landing.Settings.ShouldNotContainKey("outDir");
        landing.Settings["fontSize"].ShouldBe("13");
    }

    [Fact]
    public void Parse_MacroSection_CollectsTemplates()
    {
        var toml = """
            [macro]
            showcase = "dotnet run --project Spectre.Docs.Examples --no-build showcase {0}"
            """;

        var config = VcrConfigReader.Parse(toml);
        config.Macros["showcase"].ShouldBe("dotnet run --project Spectre.Docs.Examples --no-build showcase {0}");
    }

    [Fact]
    public void Parse_CommentsAndBlankLines_AreIgnored_ButHashInStringsPreserved()
    {
        var toml = """
            # house style
            [preset.doc]
            theme = "one dark"   # inline comment

            marginFill = "#ff0000"
            """;

        var doc = VcrConfigReader.Parse(toml).Presets["doc"];
        doc.Settings["theme"].ShouldBe("one dark");
        doc.Settings["marginFill"].ShouldBe("#ff0000");
    }

    [Fact]
    public void Parse_UnknownSection_Throws()
    {
        Should.Throw<VcrConfigException>(() => VcrConfigReader.Parse("[bogus]\nx = 1"));
    }

    [Fact]
    public void Parse_KeyBeforeSection_Throws()
    {
        Should.Throw<VcrConfigException>(() => VcrConfigReader.Parse("theme = \"x\""));
    }

    [Fact]
    public void Parse_SamePresetAcrossSections_IsMerged()
    {
        var toml = """
            [preset.doc]
            cols = 80
            [preset.doc]
            rows = 10
            """;

        var doc = VcrConfigReader.Parse(toml).Presets["doc"];
        doc.Settings["cols"].ShouldBe("80");
        doc.Settings["rows"].ShouldBe("10");
    }
}
