using Shouldly;
using VcrSharp.Core.Parsing;
using VcrSharp.Core.Parsing.Ast;

namespace VcrSharp.Core.Tests.Parsing.TapeParserTests;

/// <summary>
/// Tests for tokenizer edge cases where keywords appear at the start of setting names.
/// This ensures that compound identifiers like "EndBuffer" are correctly tokenized as
/// identifiers rather than being split into keyword+identifier tokens.
/// </summary>
public class TokenizerEdgeCaseTests
{
    [Fact]
    public void ParseTape_SetEndBuffer_ParsesAsIdentifier()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Set EndBuffer 100ms";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<SetCommand>();
        cmd.SettingName.ShouldBe("EndBuffer");
        cmd.Value.ShouldBe("00:00:00.1000000");
    }

    [Fact]
    public void ParseTape_SetStartBuffer_ParsesAsIdentifier()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Set StartBuffer 50ms";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<SetCommand>();
        cmd.SettingName.ShouldBe("StartBuffer");
        cmd.Value.ShouldBe("00:00:00.0500000");
    }

    [Fact]
    public void ParseTape_SetWaitTimeout_ParsesAsIdentifier()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Set WaitTimeout 5s";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<SetCommand>();
        cmd.SettingName.ShouldBe("WaitTimeout");
        cmd.Value.ShouldBe("00:00:05");
    }

    [Fact]
    public void ParseTape_SetWaitPattern_ParsesAsIdentifier()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Set WaitPattern \"^\\\\$\"";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<SetCommand>();
        cmd.SettingName.ShouldBe("WaitPattern");
        cmd.Value.ShouldBe("^\\$");
    }

    [Fact]
    public void ParseTape_SetStartWaitTimeout_ParsesAsIdentifier()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Set StartWaitTimeout 2s";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<SetCommand>();
        cmd.SettingName.ShouldBe("StartWaitTimeout");
        cmd.Value.ShouldBe("00:00:02");
    }

    [Fact]
    public void ParseTape_SetInactivityTimeout_ParsesAsIdentifier()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Set InactivityTimeout 3s";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<SetCommand>();
        cmd.SettingName.ShouldBe("InactivityTimeout");
        cmd.Value.ShouldBe("00:00:03");
    }

    // Ensure standalone keywords still work correctly

    [Fact]
    public void ParseTape_StandaloneEndKey_ParsesAsKeyCommand()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "End";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<KeyCommand>();
        cmd.KeyName.ShouldBe("End");
    }

    [Fact]
    public void ParseTape_StandaloneWaitKey_ParsesAsWaitCommand()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Wait";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        commands[0].ShouldBeOfType<WaitCommand>();
    }

    [Fact]
    public void ParseTape_StandaloneEnterKey_ParsesAsKeyCommand()
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
    }

    [Fact]
    public void ParseTape_StandaloneHomeKey_ParsesAsKeyCommand()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Home";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<KeyCommand>();
        cmd.KeyName.ShouldBe("Home");
    }

    [Fact]
    public void ParseTape_MultipleCommandsWithIdentifiersAndKeywords_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = @"
Set EndBuffer 100ms
Set StartBuffer 50ms
Type ""test""
End
Wait
";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(5);

        var cmd1 = commands[0].ShouldBeOfType<SetCommand>();
        cmd1.SettingName.ShouldBe("EndBuffer");

        var cmd2 = commands[1].ShouldBeOfType<SetCommand>();
        cmd2.SettingName.ShouldBe("StartBuffer");

        commands[2].ShouldBeOfType<TypeCommand>();

        var cmd4 = commands[3].ShouldBeOfType<KeyCommand>();
        cmd4.KeyName.ShouldBe("End");

        commands[4].ShouldBeOfType<WaitCommand>();
    }

    // Test other special keys to ensure they still work

    [Fact]
    public void ParseTape_SpecialKeys_ParseCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Space Tab Backspace Delete Insert Escape PageUp PageDown Up Down Left Right";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(12);
        commands[0].ShouldBeOfType<KeyCommand>().KeyName.ShouldBe("Space");
        commands[1].ShouldBeOfType<KeyCommand>().KeyName.ShouldBe("Tab");
        commands[2].ShouldBeOfType<KeyCommand>().KeyName.ShouldBe("Backspace");
        commands[3].ShouldBeOfType<KeyCommand>().KeyName.ShouldBe("Delete");
        commands[4].ShouldBeOfType<KeyCommand>().KeyName.ShouldBe("Insert");
        commands[5].ShouldBeOfType<KeyCommand>().KeyName.ShouldBe("Escape");
        commands[6].ShouldBeOfType<KeyCommand>().KeyName.ShouldBe("PageUp");
        commands[7].ShouldBeOfType<KeyCommand>().KeyName.ShouldBe("PageDown");
        commands[8].ShouldBeOfType<KeyCommand>().KeyName.ShouldBe("Up");
        commands[9].ShouldBeOfType<KeyCommand>().KeyName.ShouldBe("Down");
        commands[10].ShouldBeOfType<KeyCommand>().KeyName.ShouldBe("Left");
        commands[11].ShouldBeOfType<KeyCommand>().KeyName.ShouldBe("Right");
    }
}
