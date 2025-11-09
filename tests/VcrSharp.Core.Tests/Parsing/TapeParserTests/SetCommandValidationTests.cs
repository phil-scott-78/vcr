using Shouldly;
using VcrSharp.Core.Parsing;
using VcrSharp.Core.Parsing.Ast;

namespace VcrSharp.Core.Tests.Parsing.TapeParserTests;

/// <summary>
/// Tests for TapeParser SET command validation rules.
/// </summary>
public class SetCommandValidationTests
{
    [Fact]
    public void ParseTape_DuplicateSetCommand_ThrowsTapeParseException()
    {
        // Arrange
        var parser = new TapeParser();
        var source = """
            Set FontSize 32
            Set Theme "Dracula"
            Set FontSize 24
            """;

        // Act & Assert
        var exception = Should.Throw<TapeParseException>(() => parser.ParseTape(source));
        exception.Message.ShouldContain("Duplicate SET command");
        exception.Message.ShouldContain("FontSize");
        exception.Line.ShouldBe(3);
    }

    [Fact]
    public void ParseTape_DuplicateSetCommandCaseInsensitive_ThrowsTapeParseException()
    {
        // Arrange
        var parser = new TapeParser();
        var source = """
            Set FontSize 32
            Set fontsize 24
            """;

        // Act & Assert
        var exception = Should.Throw<TapeParseException>(() => parser.ParseTape(source));
        exception.Message.ShouldContain("Duplicate SET command");
        exception.Message.ShouldContain("fontsize");
        exception.Line.ShouldBe(2);
    }

    [Fact]
    public void ParseTape_SetAfterTypeCommand_ThrowsTapeParseException()
    {
        // Arrange
        var parser = new TapeParser();
        var source = """
            Set FontSize 32
            Type "hello"
            Set Theme "Dracula"
            """;

        // Act & Assert
        var exception = Should.Throw<TapeParseException>(() => parser.ParseTape(source));
        exception.Message.ShouldContain("appears after action commands");
        exception.Message.ShouldContain("Theme");
        exception.Line.ShouldBe(3);
    }

    [Fact]
    public void ParseTape_SetAfterExecCommand_ThrowsTapeParseException()
    {
        // Arrange
        var parser = new TapeParser();
        var source = """
            Set Shell "bash"
            Exec "ls -la"
            Set FontSize 32
            """;

        // Act & Assert
        var exception = Should.Throw<TapeParseException>(() => parser.ParseTape(source));
        exception.Message.ShouldContain("appears after action commands");
        exception.Message.ShouldContain("FontSize");
        exception.Line.ShouldBe(3);
    }

    [Fact]
    public void ParseTape_SetAfterSleepCommand_ThrowsTapeParseException()
    {
        // Arrange
        var parser = new TapeParser();
        var source = """
            Sleep 1s
            Set FontSize 32
            """;

        // Act & Assert
        var exception = Should.Throw<TapeParseException>(() => parser.ParseTape(source));
        exception.Message.ShouldContain("appears after action commands");
        exception.Message.ShouldContain("FontSize");
        exception.Line.ShouldBe(2);
    }

    [Fact]
    public void ParseTape_SetAfterWaitCommand_ThrowsTapeParseException()
    {
        // Arrange
        var parser = new TapeParser();
        var source = """
            Wait
            Set FontSize 32
            """;

        // Act & Assert
        var exception = Should.Throw<TapeParseException>(() => parser.ParseTape(source));
        exception.Message.ShouldContain("appears after action commands");
        exception.Line.ShouldBe(2);
    }

    [Fact]
    public void ParseTape_SetAfterKeyCommand_ThrowsTapeParseException()
    {
        // Arrange
        var parser = new TapeParser();
        var source = """
            Enter
            Set FontSize 32
            """;

        // Act & Assert
        var exception = Should.Throw<TapeParseException>(() => parser.ParseTape(source));
        exception.Message.ShouldContain("appears after action commands");
        exception.Line.ShouldBe(2);
    }

    [Fact]
    public void ParseTape_SetAfterModifierCommand_ThrowsTapeParseException()
    {
        // Arrange
        var parser = new TapeParser();
        var source = """
            Ctrl+C
            Set FontSize 32
            """;

        // Act & Assert
        var exception = Should.Throw<TapeParseException>(() => parser.ParseTape(source));
        exception.Message.ShouldContain("appears after action commands");
        exception.Line.ShouldBe(2);
    }

    [Fact]
    public void ParseTape_SetAfterHideCommand_ThrowsTapeParseException()
    {
        // Arrange
        var parser = new TapeParser();
        var source = """
            Hide
            Set FontSize 32
            """;

        // Act & Assert
        var exception = Should.Throw<TapeParseException>(() => parser.ParseTape(source));
        exception.Message.ShouldContain("appears after action commands");
        exception.Line.ShouldBe(2);
    }

    [Fact]
    public void ParseTape_SetAfterShowCommand_ThrowsTapeParseException()
    {
        // Arrange
        var parser = new TapeParser();
        var source = """
            Show
            Set FontSize 32
            """;

        // Act & Assert
        var exception = Should.Throw<TapeParseException>(() => parser.ParseTape(source));
        exception.Message.ShouldContain("appears after action commands");
        exception.Line.ShouldBe(2);
    }

    [Fact]
    public void ParseTape_SetAfterScreenshotCommand_ThrowsTapeParseException()
    {
        // Arrange
        var parser = new TapeParser();
        var source = """
            Screenshot test.png
            Set FontSize 32
            """;

        // Act & Assert
        var exception = Should.Throw<TapeParseException>(() => parser.ParseTape(source));
        exception.Message.ShouldContain("appears after action commands");
        exception.Line.ShouldBe(2);
    }

    [Fact]
    public void ParseTape_SetAfterCopyCommand_ThrowsTapeParseException()
    {
        // Arrange
        var parser = new TapeParser();
        var source = """
            Copy "text"
            Set FontSize 32
            """;

        // Act & Assert
        var exception = Should.Throw<TapeParseException>(() => parser.ParseTape(source));
        exception.Message.ShouldContain("appears after action commands");
        exception.Line.ShouldBe(2);
    }

    [Fact]
    public void ParseTape_SetAfterPasteCommand_ThrowsTapeParseException()
    {
        // Arrange
        var parser = new TapeParser();
        var source = """
            Paste
            Set FontSize 32
            """;

        // Act & Assert
        var exception = Should.Throw<TapeParseException>(() => parser.ParseTape(source));
        exception.Message.ShouldContain("appears after action commands");
        exception.Line.ShouldBe(2);
    }

    [Fact]
    public void ParseTape_SetBeforeNonActionCommands_Succeeds()
    {
        // Arrange
        var parser = new TapeParser();
        var source = """
            Set FontSize 32
            Output test.gif
            Require npm
            Env VAR "value"
            Set Theme "Dracula"
            """;

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(5);
        commands[0].ShouldBeOfType<SetCommand>();
        commands[1].ShouldBeOfType<OutputCommand>();
        commands[2].ShouldBeOfType<RequireCommand>();
        commands[3].ShouldBeOfType<EnvCommand>();
        commands[4].ShouldBeOfType<SetCommand>();
    }

    [Fact]
    public void ParseTape_MultipleUniqueSetCommands_Succeeds()
    {
        // Arrange
        var parser = new TapeParser();
        var source = """
            Set FontSize 32
            Set Theme "Dracula"
            Set Shell "bash"
            Set Width 1200
            Set Height 800
            """;

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(5);
        commands.ShouldAllBe(c => c is SetCommand);
    }

    [Fact]
    public void ParseTape_ValidTapeWithSetBeforeActions_Succeeds()
    {
        // Arrange
        var parser = new TapeParser();
        var source = """
            # Configuration
            Set FontSize 32
            Set Theme "Dracula"
            Output demo.gif

            # Actions
            Type "hello world"
            Enter
            Sleep 1s
            """;

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(6);
        commands[0].ShouldBeOfType<SetCommand>();
        commands[1].ShouldBeOfType<SetCommand>();
        commands[2].ShouldBeOfType<OutputCommand>();
        commands[3].ShouldBeOfType<TypeCommand>();
        commands[4].ShouldBeOfType<KeyCommand>();
        commands[5].ShouldBeOfType<SleepCommand>();
    }

    [Fact]
    public void ParseTape_DuplicateWithDifferentCasing_ThrowsException()
    {
        // Arrange
        var parser = new TapeParser();
        var source = """
            Set SHELL "bash"
            Set Shell "zsh"
            """;

        // Act & Assert
        var exception = Should.Throw<TapeParseException>(() => parser.ParseTape(source));
        exception.Message.ShouldContain("Duplicate SET command");
        exception.Message.ShouldContain("Shell");
    }

    [Fact]
    public void ParseTape_OutputAndEnvAfterSet_AllowedBeforeActions()
    {
        // Arrange
        var parser = new TapeParser();
        var source = """
            Set FontSize 32
            Output demo.gif
            Env VAR "value"
            Require npm
            Source other.tape
            Type "test"
            """;

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(6);
        // Output, Env, Require, Source are not subject to SET validation rules
        // Only SET commands must be unique and before actions
    }
}