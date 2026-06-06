using Shouldly;
using VcrSharp.Core.Parsing;
using VcrSharp.Core.Session;
using VcrSharp.Core.Settings;

namespace VcrSharp.Core.Tests.Settings;

/// <summary>Tests for the Phase-2 forward-name aliases and deprecation warnings.</summary>
public class SettingDeprecationsTests
{
    private static readonly TapeParser Parser = new();

    [Fact]
    public void HoldDuration_IsAnAliasForEndBuffer()
    {
        SessionOptions.FromCommands(Parser.ParseTape("Set HoldDuration 3s")).EndBuffer.ShouldBe(TimeSpan.FromSeconds(3));
        // The legacy name still works identically.
        SessionOptions.FromCommands(Parser.ParseTape("Set EndBuffer 3s")).EndBuffer.ShouldBe(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void Animate_IsInverseOfStaticOutput()
    {
        SessionOptions.FromCommands(Parser.ParseTape("Set Animate false")).StaticOutput.ShouldBeTrue();
        SessionOptions.FromCommands(Parser.ParseTape("Set Animate true")).StaticOutput.ShouldBeFalse();
    }

    [Fact]
    public void Collect_FlagsDeprecatedSettings_WithReplacementGuidance()
    {
        var warnings = SettingDeprecations.Collect(Parser.ParseTape("Set Margin 10\nSet Width 1200\nExec \"x\""));
        warnings.ShouldContain(w => w.Contains("Margin") && w.Contains("no effect on SVG"));
        warnings.ShouldContain(w => w.Contains("Width") && w.Contains("Cols"));
    }

    [Fact]
    public void Collect_FlagsDeprecatedCommands()
    {
        SettingDeprecations.Collect(Parser.ParseTape("Source other.tape"))
            .ShouldContain(w => w.Contains("Source") && w.Contains("Use"));
        SettingDeprecations.Collect(Parser.ParseTape("Require npm"))
            .ShouldContain(w => w.Contains("Require"));
    }

    [Fact]
    public void Collect_LeavesLiveSettingsAlone()
    {
        SettingDeprecations.Collect(Parser.ParseTape("Set Cols 80\nSet Rows 20\nSet HoldDuration 2s\nUse doc\nExec \"x\""))
            .ShouldBeEmpty();
    }

    [Fact]
    public void Collect_StaticOutput_PointsAtAnimate()
    {
        SettingDeprecations.Collect(Parser.ParseTape("Set StaticOutput true\nExec \"x\""))
            .ShouldContain(w => w.Contains("StaticOutput") && w.Contains("Animate"));
    }
}
