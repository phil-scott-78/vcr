using Shouldly;
using VcrSharp.Core.Parsing;
using VcrSharp.Core.Parsing.Ast;

namespace VcrSharp.Core.Tests.Parsing.TapeParserTests;

/// <summary>
/// Tests for TapeParser Screenshot command parsing.
/// </summary>
public class ScreenshotCommandTests
{
    [Fact]
    public void ParseTape_ScreenshotCommandWithPng_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Screenshot output.png";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<ScreenshotCommand>();
        cmd.FilePath.ShouldBe("output.png");
    }

    [Fact]
    public void ParseTape_ScreenshotCommandWithSvg_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Screenshot output.svg";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<ScreenshotCommand>();
        cmd.FilePath.ShouldBe("output.svg");
    }

    [Fact]
    public void ParseTape_ScreenshotCommandWithPath_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Screenshot \"samples/screenshot.svg\"";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<ScreenshotCommand>();
        cmd.FilePath.ShouldBe("samples/screenshot.svg");
    }

    [Fact]
    public void ScreenshotCommand_ToString_FormatsCorrectly()
    {
        // Arrange
        var cmd = new ScreenshotCommand("output.svg");

        // Act
        var result = cmd.ToString();

        // Assert
        result.ShouldBe("Screenshot output.svg");
    }
}
