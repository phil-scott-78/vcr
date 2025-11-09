using Shouldly;
using VcrSharp.Core.Parsing;
using VcrSharp.Core.Parsing.Ast;

namespace VcrSharp.Core.Tests.Parsing.TapeParserTests;

/// <summary>
/// Tests for TapeParser action command parsing.
/// </summary>
public class ActionCommandTests
{
    [Fact]
    public void ParseTape_TypeCommand_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Type \"hello world\"";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<TypeCommand>();
        cmd.Text.ShouldBe("hello world");
        cmd.Speed.ShouldBeNull();
    }

    [Fact]
    public void ParseTape_TypeCommandWithSpeed_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Type@500ms \"slow typing\"";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<TypeCommand>();
        cmd.Text.ShouldBe("slow typing");
        cmd.Speed.ShouldNotBeNull();
        cmd.Speed.Value.TotalMilliseconds.ShouldBe(500);
    }

    [Fact]
    public void ParseTape_SleepCommand_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Sleep 1s";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<SleepCommand>();
        cmd.Duration.TotalSeconds.ShouldBe(1);
    }

    [Fact]
    public void ParseTape_SleepCommandWithMilliseconds_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Sleep 500ms";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<SleepCommand>();
        cmd.Duration.TotalMilliseconds.ShouldBe(500);
    }

    [Fact]
    public void ParseTape_SleepCommandWithBareNumber_ParsesAsSeconds()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Sleep 2";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<SleepCommand>();
        cmd.Duration.TotalSeconds.ShouldBe(2);
    }

    [Fact]
    public void ParseTape_EnterCommand_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Enter";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<KeyCommand>();
        cmd.KeyName.ShouldBe("Enter");
        cmd.RepeatCount.ShouldBe(1);
        cmd.Speed.ShouldBeNull();
    }

    [Fact]
    public void ParseTape_EnterCommandWithRepeat_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Enter 3";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<KeyCommand>();
        cmd.KeyName.ShouldBe("Enter");
        cmd.RepeatCount.ShouldBe(3);
    }

    [Fact]
    public void ParseTape_BackspaceCommandWithSpeed_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Backspace@100ms";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<KeyCommand>();
        cmd.KeyName.ShouldBe("Backspace");
        cmd.Speed.ShouldNotBeNull();
        cmd.Speed.Value.TotalMilliseconds.ShouldBe(100);
    }

    [Fact]
    public void ParseTape_BackspaceCommandWithSpeedAndRepeat_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Backspace@100ms 5";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<KeyCommand>();
        cmd.KeyName.ShouldBe("Backspace");
        cmd.RepeatCount.ShouldBe(5);
        cmd.Speed!.Value.TotalMilliseconds.ShouldBe(100);
    }

    [Theory]
    [InlineData("Space", "Space")]
    [InlineData("Tab", "Tab")]
    [InlineData("Backspace", "Backspace")]
    [InlineData("Delete", "Delete")]
    [InlineData("Insert", "Insert")]
    [InlineData("Escape", "Escape")]
    [InlineData("Up", "Up")]
    [InlineData("Down", "Down")]
    [InlineData("Left", "Left")]
    [InlineData("Right", "Right")]
    [InlineData("PageUp", "PageUp")]
    [InlineData("PageDown", "PageDown")]
    [InlineData("Home", "Home")]
    [InlineData("End", "End")]
    public void ParseTape_KeyCommands_ParseCorrectly(string keyName, string expectedKeyName)
    {
        // Arrange
        var parser = new TapeParser();
        var source = keyName;

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<KeyCommand>();
        cmd.KeyName.ShouldBe(expectedKeyName);
    }
}