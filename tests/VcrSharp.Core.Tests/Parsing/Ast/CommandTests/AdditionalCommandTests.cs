using Shouldly;
using VcrSharp.Core.Parsing.Ast;

namespace VcrSharp.Core.Tests.Parsing.Ast.CommandTests;

/// <summary>
/// Tests for additional Command AST classes.
/// </summary>
public class AdditionalCommandTests
{
    [Fact]
    public void SetCommand_Construction_SetsPropertiesCorrectly()
    {
        // Arrange & Act
        var cmd = new SetCommand("FontSize", "32");

        // Assert
        cmd.SettingName.ShouldBe("FontSize");
        cmd.Value.ShouldBe("32");
    }

    [Fact]
    public void OutputCommand_Construction_SetsPropertiesCorrectly()
    {
        // Arrange & Act
        var cmd = new OutputCommand("demo.gif");

        // Assert
        cmd.FilePath.ShouldBe("demo.gif");
    }

    [Fact]
    public void ExecCommand_Construction_SetsPropertiesCorrectly()
    {
        // Arrange & Act
        var cmd = new ExecCommand("npm install");

        // Assert
        cmd.Command.ShouldBe("npm install");
    }

    [Fact]
    public void EnvCommand_Construction_SetsPropertiesCorrectly()
    {
        // Arrange & Act
        var cmd = new EnvCommand("USER", "developer");

        // Assert
        cmd.Key.ShouldBe("USER");
        cmd.Value.ShouldBe("developer");
    }

    [Fact]
    public void ScreenshotCommand_Construction_SetsPropertiesCorrectly()
    {
        // Arrange & Act
        var cmd = new ScreenshotCommand("output.png");

        // Assert
        cmd.FilePath.ShouldBe("output.png");
    }
}