using Shouldly;
using VcrSharp.Core.Parsing;
using VcrSharp.Core.Parsing.Ast;

namespace VcrSharp.Core.Tests.Parsing.TapeParserTests;

/// <summary>
/// Tests for TapeParser exec command parsing.
/// </summary>
public class ExecCommandTests
{
    [Fact]
    public void ParseTape_ExecCommand_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Exec \"npm install\"";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<ExecCommand>();
        cmd.Command.ShouldBe("npm install");
    }

    [Fact]
    public void ParseTape_ExecCommandWithComplexCommand_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Exec \"git status\"";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<ExecCommand>();
        cmd.Command.ShouldBe("git status");
    }
}