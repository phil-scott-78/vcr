using System.Text.RegularExpressions;
using Shouldly;
using VcrSharp.Core.Parsing.Ast;

namespace VcrSharp.Core.Tests.Parsing.Ast.CommandTests;

/// <summary>
/// Tests for Command AST class construction.
/// </summary>
public class CommandConstructionTests
{
    [Fact]
    public void TypeCommand_Construction_SetsPropertiesCorrectly()
    {
        // Arrange & Act
        var cmd = new TypeCommand("hello world");

        // Assert
        cmd.Text.ShouldBe("hello world");
        cmd.Speed.ShouldBeNull();
    }

    [Fact]
    public void TypeCommand_ConstructionWithSpeed_SetsPropertiesCorrectly()
    {
        // Arrange & Act
        var cmd = new TypeCommand("text", TimeSpan.FromMilliseconds(500));

        // Assert
        cmd.Text.ShouldBe("text");
        cmd.Speed.ShouldNotBeNull();
        cmd.Speed.Value.TotalMilliseconds.ShouldBe(500);
    }

    [Fact]
    public void KeyCommand_Construction_SetsPropertiesCorrectly()
    {
        // Arrange & Act
        var cmd = new KeyCommand("Enter");

        // Assert
        cmd.KeyName.ShouldBe("Enter");
        cmd.RepeatCount.ShouldBe(1);
        cmd.Speed.ShouldBeNull();
    }

    [Fact]
    public void KeyCommand_ConstructionWithRepeat_SetsPropertiesCorrectly()
    {
        // Arrange & Act
        var cmd = new KeyCommand("Backspace", repeatCount: 5);

        // Assert
        cmd.KeyName.ShouldBe("Backspace");
        cmd.RepeatCount.ShouldBe(5);
    }

    [Fact]
    public void KeyCommand_ConstructionWithSpeedAndRepeat_SetsPropertiesCorrectly()
    {
        // Arrange & Act
        var cmd = new KeyCommand("Delete", repeatCount: 3, speed: TimeSpan.FromMilliseconds(100));

        // Assert
        cmd.KeyName.ShouldBe("Delete");
        cmd.RepeatCount.ShouldBe(3);
        cmd.Speed!.Value.TotalMilliseconds.ShouldBe(100);
    }

    [Fact]
    public void KeyCommand_NegativeRepeatCount_ClampsToOne()
    {
        // Arrange & Act
        var cmd = new KeyCommand("Enter", repeatCount: -5);

        // Assert
        cmd.RepeatCount.ShouldBe(1);
    }

    [Fact]
    public void KeyCommand_ZeroRepeatCount_ClampsToOne()
    {
        // Arrange & Act
        var cmd = new KeyCommand("Enter", repeatCount: 0);

        // Assert
        cmd.RepeatCount.ShouldBe(1);
    }

    [Fact]
    public void ModifierCommand_Construction_SetsPropertiesCorrectly()
    {
        // Arrange & Act
        var cmd = new ModifierCommand(hasCtrl: true, hasAlt: false, hasShift: false, key: "C");

        // Assert
        cmd.HasCtrl.ShouldBeTrue();
        cmd.HasAlt.ShouldBeFalse();
        cmd.HasShift.ShouldBeFalse();
        cmd.Key.ShouldBe("C");
    }

    [Fact]
    public void ModifierCommand_MultipleModifiers_SetsPropertiesCorrectly()
    {
        // Arrange & Act
        var cmd = new ModifierCommand(hasCtrl: true, hasAlt: true, hasShift: true, key: "Delete");

        // Assert
        cmd.HasCtrl.ShouldBeTrue();
        cmd.HasAlt.ShouldBeTrue();
        cmd.HasShift.ShouldBeTrue();
        cmd.Key.ShouldBe("Delete");
    }

    [Fact]
    public void WaitCommand_ConstructionWithScope_SetsPropertiesCorrectly()
    {
        // Arrange & Act
        var cmd = new WaitCommand(scope: WaitScope.Screen);

        // Assert
        cmd.Scope.ShouldBe(WaitScope.Screen);
    }

    [Fact]
    public void WaitCommand_ConstructionWithAllParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        var timeout = TimeSpan.FromSeconds(5);
        var pattern = new Regex("pattern");

        // Act
        var cmd = new WaitCommand(scope: WaitScope.Screen, timeout: timeout, pattern: pattern);

        // Assert
        cmd.Scope.ShouldBe(WaitScope.Screen);
        cmd.Timeout.ShouldBe(timeout);
        cmd.Pattern.ShouldBe(pattern);
    }

    [Fact]
    public void SleepCommand_Construction_SetsPropertiesCorrectly()
    {
        // Arrange & Act
        var cmd = new SleepCommand(TimeSpan.FromSeconds(2));

        // Assert
        cmd.Duration.TotalSeconds.ShouldBe(2);
    }
}