using Shouldly;
using VcrSharp.Core.Parsing;
using VcrSharp.Core.Parsing.Ast;

namespace VcrSharp.Core.Tests.Parsing.TapeParserTests;

/// <summary>
/// Tests for TapeParser control command parsing.
/// </summary>
public class ControlCommandTests
{
    [Fact]
    public void ParseTape_HideCommand_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Hide";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        commands[0].ShouldBeOfType<HideCommand>();
    }

    [Fact]
    public void ParseTape_ShowCommand_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Show";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        commands[0].ShouldBeOfType<ShowCommand>();
    }

    [Fact]
    public void ParseTape_ScreenshotCommand_ParsesCorrectly()
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
}