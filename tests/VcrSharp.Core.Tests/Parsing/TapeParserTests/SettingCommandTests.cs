using Shouldly;
using VcrSharp.Core.Parsing;
using VcrSharp.Core.Parsing.Ast;

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

    [Fact]
    public void ParseTape_RequireCommand_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Require npm";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<RequireCommand>();
        cmd.ProgramName.ShouldBe("npm");
    }

    [Fact]
    public void ParseTape_SourceCommand_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Source script.tape";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<SourceCommand>();
        cmd.FilePath.ShouldBe("script.tape");
    }
}