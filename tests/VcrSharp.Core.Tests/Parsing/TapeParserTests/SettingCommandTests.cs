using Shouldly;
using VcrSharp.Core.Parsing;
using VcrSharp.Core.Parsing.Ast;
using VcrSharp.Core.Session;

namespace VcrSharp.Core.Tests.Parsing.TapeParserTests;

/// <summary>
/// Tests for TapeParser setting command parsing.
/// </summary>
public class SettingCommandTests
{
    [Fact]
    public void ParseTape_SetCommandWithString_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Set Theme \"Dracula\"";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<SetCommand>();
        cmd.SettingName.ShouldBe("Theme");
        cmd.Value.ShouldBe("Dracula");
    }

    [Fact]
    public void ParseTape_SetCommandWithNumber_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Set FontSize 32";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<SetCommand>();
        cmd.SettingName.ShouldBe("FontSize");
        cmd.Value.ShouldBe("32");
    }

    [Fact]
    public void ParseTape_SetCommandWithDuration_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Set TypingSpeed 100ms";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<SetCommand>();
        cmd.SettingName.ShouldBe("TypingSpeed");
        cmd.Value.ShouldBe("00:00:00.1000000");
    }

    [Fact]
    public void ParseTape_SetStartupDelay_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Set StartupDelay 5s";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<SetCommand>();
        cmd.SettingName.ShouldBe("StartupDelay");
        cmd.Value.ShouldBe("00:00:05"); // TimeSpan format
    }

    [Fact]
    public void ParseTape_SetCommandWithBoolean_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Set CursorBlink true";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<SetCommand>();
        cmd.SettingName.ShouldBe("CursorBlink");
        cmd.Value.ShouldBe("True");
    }

    [Fact]
    public void ParseTape_SetDisableCursorTrue_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Set DisableCursor true";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<SetCommand>();
        cmd.SettingName.ShouldBe("DisableCursor");
        cmd.Value.ShouldBe("True");
    }

    [Fact]
    public void ParseTape_SetDisableCursorFalse_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Set DisableCursor false";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<SetCommand>();
        cmd.SettingName.ShouldBe("DisableCursor");
        cmd.Value.ShouldBe("False");
    }

    [Fact]
    public void ParseTape_SetTransparentBackground_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Set TransparentBackground true";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<SetCommand>();
        cmd.SettingName.ShouldBe("TransparentBackground");
        cmd.Value.ShouldBe("True");
    }

    [Fact]
    public void ParseTape_SetSvgMetadataAndIntrinsicSize_ParsesAndApplies()
    {
        var parser = new TapeParser();

        // Defaults are ON.
        new SessionOptions().SvgMetadata.ShouldBeTrue();
        new SessionOptions().SvgIntrinsicSize.ShouldBeTrue();

        SessionOptions.FromCommands(parser.ParseTape("Set SvgMetadata false")).SvgMetadata.ShouldBeFalse();
        SessionOptions.FromCommands(parser.ParseTape("Set SvgIntrinsicSize false")).SvgIntrinsicSize.ShouldBeFalse();
    }

    [Fact]
    public void ParseTape_SetFitToContent_ParsesAndApplies()
    {
        var parser = new TapeParser();
        new SessionOptions().FitToContent.ShouldBeFalse(); // default
        SessionOptions.FromCommands(parser.ParseTape("Set FitToContent true")).FitToContent.ShouldBeTrue();
        SessionOptions.FromCommands(parser.ParseTape("Set FitToContent false")).FitToContent.ShouldBeFalse();
    }

    [Fact]
    public void ParseTape_SetLoopAndLoopCount_ParsesAndApplies()
    {
        var parser = new TapeParser();
        new SessionOptions().Loop.ShouldBeTrue();           // default infinite
        new SessionOptions().LoopCount.ShouldBeNull();

        SessionOptions.FromCommands(parser.ParseTape("Set Loop false")).Loop.ShouldBeFalse();
        SessionOptions.FromCommands(parser.ParseTape("Set LoopCount 3")).LoopCount.ShouldBe(3);
    }

    [Fact]
    public void Validate_LoopCountZero_IsRejected()
    {
        var parser = new TapeParser();
        var options = SessionOptions.FromCommands(parser.ParseTape("Set LoopCount 0\nOutput x.svg"));
        options.Validate().ShouldContain("LoopCount must be greater than 0");

        var ok = SessionOptions.FromCommands(parser.ParseTape("Set LoopCount 2\nOutput x.svg"));
        ok.Validate().ShouldNotContain("LoopCount must be greater than 0");
    }

    [Fact]
    public void ParseTape_SetScreenshotAndStaticOutputSettings_ParseAndApply()
    {
        var parser = new TapeParser();

        new SessionOptions().ScreenshotWaitForInactivity.ShouldBeFalse(); // default
        new SessionOptions().StaticOutput.ShouldBeFalse();
        new SessionOptions().ScreenshotInactivityTimeout.ShouldBe(TimeSpan.FromMilliseconds(500));

        SessionOptions.FromCommands(parser.ParseTape("Set ScreenshotWaitForInactivity true"))
            .ScreenshotWaitForInactivity.ShouldBeTrue();
        SessionOptions.FromCommands(parser.ParseTape("Set ScreenshotInactivityTimeout 250ms"))
            .ScreenshotInactivityTimeout.ShouldBe(TimeSpan.FromMilliseconds(250));
        SessionOptions.FromCommands(parser.ParseTape("Set StaticOutput true"))
            .StaticOutput.ShouldBeTrue();
    }

    [Fact]
    public void Validate_StaticOutput_RequiresSvgOrPng()
    {
        var parser = new TapeParser();

        var bad = SessionOptions.FromCommands(parser.ParseTape("Set StaticOutput true\nOutput demo.gif"));
        bad.Validate().ShouldContain("StaticOutput requires .svg or .png output files; got 'demo.gif'");

        var svg = SessionOptions.FromCommands(parser.ParseTape("Set StaticOutput true\nOutput demo.svg"));
        svg.Validate().Where(e => e.Contains("StaticOutput")).ShouldBeEmpty();

        var png = SessionOptions.FromCommands(parser.ParseTape("Set StaticOutput true\nOutput demo.png"));
        png.Validate().Where(e => e.Contains("StaticOutput")).ShouldBeEmpty();
    }

    [Fact]
    public void ParseTape_OutputCommand_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Output demo.gif";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<OutputCommand>();
        cmd.FilePath.ShouldBe("demo.gif");
    }

    [Fact]
    public void ParseTape_OutputCommandWithQuotedPath_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Output \"path with spaces.mp4\"";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<OutputCommand>();
        cmd.FilePath.ShouldBe("path with spaces.mp4");
    }

}