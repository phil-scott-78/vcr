using System.Text.RegularExpressions;
using Shouldly;
using VcrSharp.Core.Parsing.Ast;

namespace VcrSharp.Core.Tests.Parsing.Ast.CommandTests;

/// <summary>
/// Tests for Command ToString() round-trip behavior.
/// </summary>
public class ToStringRoundTripTests
{
    [Fact]
    public void TypeCommand_ToString_FormatsCorrectly()
    {
        // Arrange
        var cmd = new TypeCommand("hello");

        // Act
        var result = cmd.ToString();

        // Assert
        result.ShouldBe("Type \"hello\"");
    }

    [Fact]
    public void TypeCommand_WithSpeed_ToString_FormatsCorrectly()
    {
        // Arrange
        var cmd = new TypeCommand("text", TimeSpan.FromMilliseconds(500));

        // Act
        var result = cmd.ToString();

        // Assert
        result.ShouldBe("Type@500ms \"text\"");
    }

    [Fact]
    public void KeyCommand_ToString_FormatsCorrectly()
    {
        // Arrange
        var cmd = new KeyCommand("Enter");

        // Act
        var result = cmd.ToString();

        // Assert
        result.ShouldBe("Enter");
    }

    [Fact]
    public void KeyCommand_WithRepeat_ToString_FormatsCorrectly()
    {
        // Arrange
        var cmd = new KeyCommand("Backspace", repeatCount: 5);

        // Act
        var result = cmd.ToString();

        // Assert
        result.ShouldBe("Backspace 5");
    }

    [Fact]
    public void KeyCommand_WithSpeedAndRepeat_ToString_FormatsCorrectly()
    {
        // Arrange
        var cmd = new KeyCommand("Delete", repeatCount: 3, speed: TimeSpan.FromMilliseconds(100));

        // Act
        var result = cmd.ToString();

        // Assert
        result.ShouldBe("Delete@100ms 3");
    }

    [Fact]
    public void ModifierCommand_ToString_FormatsCorrectly()
    {
        // Arrange
        var cmd = new ModifierCommand(hasCtrl: true, hasAlt: false, hasShift: false, key: "C");

        // Act
        var result = cmd.ToString();

        // Assert
        result.ShouldBe("Ctrl+C");
    }

    [Fact]
    public void ModifierCommand_WithAlt_ToString_FormatsCorrectly()
    {
        // Arrange
        var cmd = new ModifierCommand(hasCtrl: false, hasAlt: true, hasShift: false, key: "Enter");

        // Act
        var result = cmd.ToString();

        // Assert
        result.ShouldBe("Alt+Enter");
    }

    [Fact]
    public void ModifierCommand_WithAllModifiers_ToString_FormatsCorrectly()
    {
        // Arrange
        var cmd = new ModifierCommand(hasCtrl: true, hasAlt: true, hasShift: true, key: "Delete");

        // Act
        var result = cmd.ToString();

        // Assert
        result.ShouldBe("Ctrl+Alt+Shift+Delete");
    }

    [Fact]
    public void WaitCommand_Basic_ToString_FormatsCorrectly()
    {
        // Arrange
        var cmd = new WaitCommand();

        // Act
        var result = cmd.ToString();

        // Assert
        result.ShouldBe("Wait+Buffer");
    }

    [Fact]
    public void WaitCommand_WithScreen_ToString_FormatsCorrectly()
    {
        // Arrange
        var cmd = new WaitCommand(scope: WaitScope.Screen);

        // Act
        var result = cmd.ToString();

        // Assert
        result.ShouldBe("Wait+Screen");
    }

    [Fact]
    public void WaitCommand_WithTimeout_ToString_FormatsCorrectly()
    {
        // Arrange
        var cmd = new WaitCommand(timeout: TimeSpan.FromSeconds(5));

        // Act
        var result = cmd.ToString();

        // Assert
        result.ShouldBe("Wait+Buffer@5000ms");
    }

    [Fact]
    public void WaitCommand_WithPattern_ToString_FormatsCorrectly()
    {
        // Arrange
        var pattern = new Regex("pattern");
        var cmd = new WaitCommand(pattern: pattern);

        // Act
        var result = cmd.ToString();

        // Assert
        result.ShouldBe("Wait+Buffer /pattern/");
    }

    [Fact]
    public void WaitCommand_WithAllOptions_ToString_FormatsCorrectly()
    {
        // Arrange
        var pattern = new Regex("test");
        var cmd = new WaitCommand(
            scope: WaitScope.Screen,
            timeout: TimeSpan.FromMilliseconds(100),
            pattern: pattern);

        // Act
        var result = cmd.ToString();

        // Assert
        result.ShouldBe("Wait+Screen@100ms /test/");
    }
}