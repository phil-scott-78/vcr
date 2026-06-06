using Shouldly;
using VcrSharp.Core.Parsing;
using VcrSharp.Core.Session;
using VcrSharp.Core.Settings;

namespace VcrSharp.Core.Tests.Settings;

/// <summary>Tests for the Mode/Size front-ends, HoldDuration alias, and the conservative deprecation list.</summary>
public class SettingDeprecationsTests
{
    private static readonly TapeParser Parser = new();

    [Fact]
    public void Mode_MapsToStaticOutput_AnimatedIsTheDefault()
    {
        new SessionOptions().StaticOutput.ShouldBeFalse(); // default = animated
        SessionOptions.FromCommands(Parser.ParseTape("Set Mode static")).StaticOutput.ShouldBeTrue();
        SessionOptions.FromCommands(Parser.ParseTape("Set Mode animated")).StaticOutput.ShouldBeFalse();
    }

    [Fact]
    public void Size_MapsToFitToContent_GridIsTheDefault()
    {
        new SessionOptions().FitToContent.ShouldBeFalse(); // default = grid
        SessionOptions.FromCommands(Parser.ParseTape("Set Size fit")).FitToContent.ShouldBeTrue();
        SessionOptions.FromCommands(Parser.ParseTape("Set Size grid")).FitToContent.ShouldBeFalse();
    }

    [Fact]
    public void HoldDuration_IsAnAliasForEndBuffer()
    {
        SessionOptions.FromCommands(Parser.ParseTape("Set HoldDuration 3s")).EndBuffer.ShouldBe(TimeSpan.FromSeconds(3));
        SessionOptions.FromCommands(Parser.ParseTape("Set EndBuffer 3s")).EndBuffer.ShouldBe(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void Collect_FlagsRenamedSettings_PointingAtModeAndSize()
    {
        SettingDeprecations.Collect(Parser.ParseTape("Set StaticOutput true\nExec \"x\""))
            .ShouldContain(w => w.Contains("StaticOutput") && w.Contains("Mode static"));
        SettingDeprecations.Collect(Parser.ParseTape("Set FitToContent true\nExec \"x\""))
            .ShouldContain(w => w.Contains("FitToContent") && w.Contains("Size fit"));
    }

    [Fact]
    public void Collect_FlagsTrulyDeadSettings()
    {
        var warnings = SettingDeprecations.Collect(Parser.ParseTape("Set Width 1200\nSet CssVariables true\nExec \"x\""));
        warnings.ShouldContain(w => w.Contains("Width") && w.Contains("Cols"));
        warnings.ShouldContain(w => w.Contains("CssVariables"));
    }

    [Fact]
    public void Collect_DoesNotFlagAnimationRasterOrSizingSettings()
    {
        // Animation is first-class: loop/playback/palette/cursor/margin must NOT warn.
        SettingDeprecations.Collect(Parser.ParseTape(
            "Set Loop false\nSet LoopCount 3\nSet PlaybackSpeed 2\nSet MaxColors 128\nSet CursorBlink true\nSet Margin 10\nSet Framerate 30\nExec \"x\""))
            .ShouldBeEmpty();
    }

    [Fact]
    public void Collect_FlagsRemovedCommands_ButNotHideShow()
    {
        SettingDeprecations.Collect(Parser.ParseTape("Source other.tape"))
            .ShouldContain(w => w.Contains("Source") && w.Contains("Use"));
        SettingDeprecations.Collect(Parser.ParseTape("Require npm"))
            .ShouldContain(w => w.Contains("Require"));
        // Hide/Show are legitimate animation frame-gating tools — not deprecated.
        SettingDeprecations.Collect(Parser.ParseTape("Hide\nType \"x\"\nShow")).ShouldBeEmpty();
    }

    [Fact]
    public void Collect_FlagsModeAndSizeTypos()
    {
        SettingDeprecations.Collect(Parser.ParseTape("Set Mode statik\nExec \"x\""))
            .ShouldContain(w => w.Contains("Mode") && w.Contains("animated or static"));
        SettingDeprecations.Collect(Parser.ParseTape("Set Size grod\nExec \"x\""))
            .ShouldContain(w => w.Contains("Size") && w.Contains("grid or fit"));
    }

    [Fact]
    public void Collect_CleanModernTape_NoWarnings()
    {
        SettingDeprecations.Collect(Parser.ParseTape("Set Cols 80\nSet Mode static\nSet Size fit\nUse doc\nExec \"x\""))
            .ShouldBeEmpty();
    }
}
