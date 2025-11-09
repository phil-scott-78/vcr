using Shouldly;
using VcrSharp.Core.Parsing;
using VcrSharp.Core.Parsing.Ast;

namespace VcrSharp.Core.Tests.Parsing.TapeParserTests;

/// <summary>
/// Tests for TapeParser inline command parsing.
/// </summary>
public class InlineCommandTests
{
    [Fact]
    public void ParseTape_InlineCommands_ParsesMultipleCommands()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Type \"hello\" Sleep 200ms Enter";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(3);
        commands[0].ShouldBeOfType<TypeCommand>();
        commands[1].ShouldBeOfType<SleepCommand>();
        commands[2].ShouldBeOfType<KeyCommand>();
    }

    [Fact]
    public void ParseTape_InlineCommandsWithModifiers_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Type@500ms \"slow\" Sleep 1s Enter 2";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(3);

        var typeCmd = commands[0].ShouldBeOfType<TypeCommand>();
        typeCmd.Speed!.Value.TotalMilliseconds.ShouldBe(500);

        var sleepCmd = commands[1].ShouldBeOfType<SleepCommand>();
        sleepCmd.Duration.TotalSeconds.ShouldBe(1);

        var enterCmd = commands[2].ShouldBeOfType<KeyCommand>();
        enterCmd.RepeatCount.ShouldBe(2);
    }
}