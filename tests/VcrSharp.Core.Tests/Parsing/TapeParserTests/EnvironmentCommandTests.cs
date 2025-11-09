using Shouldly;
using VcrSharp.Core.Parsing;
using VcrSharp.Core.Parsing.Ast;

namespace VcrSharp.Core.Tests.Parsing.TapeParserTests;

/// <summary>
/// Tests for TapeParser environment command parsing.
/// </summary>
public class EnvironmentCommandTests
{
    [Fact]
    public void ParseTape_EnvCommand_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Env USER \"developer\"";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<EnvCommand>();
        cmd.Key.ShouldBe("USER");
        cmd.Value.ShouldBe("developer");
    }
}