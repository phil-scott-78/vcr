using Shouldly;
using VcrSharp.Core.Parsing;
using VcrSharp.Core.Parsing.Ast;

namespace VcrSharp.Core.Tests.Parsing.TapeParserTests;

/// <summary>
/// Tests for TapeParser clipboard command parsing.
/// </summary>
public class ClipboardCommandTests
{
    [Fact]
    public void ParseTape_CopyCommand_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Copy \"text to copy\"";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<CopyCommand>();
        cmd.Text.ShouldBe("text to copy");
    }

    [Fact]
    public void ParseTape_PasteCommand_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Paste";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        commands[0].ShouldBeOfType<PasteCommand>();
    }
}