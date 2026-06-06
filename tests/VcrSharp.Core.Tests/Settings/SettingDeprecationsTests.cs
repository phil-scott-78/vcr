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
    public void RemovedSettings_AreParseErrors()
    {
        // The genuinely dead names were removed from the grammar outright.
        Should.Throw<TapeParseException>(() => Parser.ParseTape("Set Width 1200"));
        Should.Throw<TapeParseException>(() => Parser.ParseTape("Set Height 800"));
        Should.Throw<TapeParseException>(() => Parser.ParseTape("Set CssVariables true"));
        Should.Throw<TapeParseException>(() => Parser.ParseTape("Set WaitPattern \"x\""));
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
    public void RemovedCommands_AreParseErrors_ButHideShowStay()
    {
        // Require/Source/Copy/Paste were removed from the grammar.
        Should.Throw<TapeParseException>(() => Parser.ParseTape("Require npm"));
        Should.Throw<TapeParseException>(() => Parser.ParseTape("Source other.tape"));
        Should.Throw<TapeParseException>(() => Parser.ParseTape("Copy \"x\""));
        Should.Throw<TapeParseException>(() => Parser.ParseTape("Paste"));
        // Hide/Show are legitimate animation frame-gating tools — they stay, with no warning.
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
