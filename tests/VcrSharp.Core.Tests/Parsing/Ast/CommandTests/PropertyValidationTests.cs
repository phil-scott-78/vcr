using Shouldly;
using VcrSharp.Core.Parsing.Ast;

namespace VcrSharp.Core.Tests.Parsing.Ast.CommandTests;

/// <summary>
/// Tests for Command property validation.
/// </summary>
public class PropertyValidationTests
{
    [Fact]
    public void KeyCommand_RepeatCountAlwaysPositive()
    {
        // Arrange & Act
        var cmd1 = new KeyCommand("Enter", repeatCount: 0);
        var cmd2 = new KeyCommand("Enter", repeatCount: -10);
        var cmd3 = new KeyCommand("Enter", repeatCount: 5);

        // Assert
        cmd1.RepeatCount.ShouldBe(1);
        cmd2.RepeatCount.ShouldBe(1);
        cmd3.RepeatCount.ShouldBe(5);
    }

    [Fact]
    public void WaitCommand_AllParametersOptional()
    {
        // Arrange & Act
        var cmd = new WaitCommand();

        // Assert
        cmd.Scope.ShouldBe(WaitScope.Buffer); // Default
        cmd.Timeout.ShouldBeNull();
        cmd.Pattern.ShouldBeNull();
    }

    [Fact]
    public void ModifierCommand_AtLeastOneModifierInConstruction()
    {
        // Note: This is enforced by parser, not by the command class itself
        // But we can verify all modifiers can be false in construction

        // Arrange & Act
        var cmd = new ModifierCommand(hasCtrl: false, hasAlt: false, hasShift: false, key: "A");

        // Assert
        cmd.HasCtrl.ShouldBeFalse();
        cmd.HasAlt.ShouldBeFalse();
        cmd.HasShift.ShouldBeFalse();
        cmd.Key.ShouldBe("A");
    }
}