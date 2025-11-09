using Shouldly;
using VcrSharp.Core.Parsing;
using VcrSharp.Core.Parsing.Ast;

namespace VcrSharp.Core.Tests.Parsing.TapeParserTests;

/// <summary>
/// Tests for TapeParser modifier command parsing.
/// </summary>
public class ModifierCommandTests
{
    [Fact]
    public void ParseTape_CtrlCCommand_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Ctrl+C";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<ModifierCommand>();
        cmd.HasCtrl.ShouldBeTrue();
        cmd.HasAlt.ShouldBeFalse();
        cmd.HasShift.ShouldBeFalse();
        cmd.Key.ShouldBe("C");
    }

    [Fact]
    public void ParseTape_AltEnterCommand_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Alt+Enter";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<ModifierCommand>();
        cmd.HasCtrl.ShouldBeFalse();
        cmd.HasAlt.ShouldBeTrue();
        cmd.HasShift.ShouldBeFalse();
        cmd.Key.ShouldBe("Enter");
    }

    [Fact]
    public void ParseTape_CtrlShiftTabCommand_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Ctrl+Shift+Tab";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<ModifierCommand>();
        cmd.HasCtrl.ShouldBeTrue();
        cmd.HasAlt.ShouldBeFalse();
        cmd.HasShift.ShouldBeTrue();
        cmd.Key.ShouldBe("Tab");
    }

    [Fact]
    public void ParseTape_CtrlAltShiftCommand_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Ctrl+Shift+Alt+A";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<ModifierCommand>();
        cmd.HasCtrl.ShouldBeTrue();
        cmd.HasAlt.ShouldBeTrue();
        cmd.HasShift.ShouldBeTrue();
        cmd.Key.ShouldBe("A");
    }
}