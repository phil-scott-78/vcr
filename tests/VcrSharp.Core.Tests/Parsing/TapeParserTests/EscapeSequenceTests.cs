using Shouldly;
using VcrSharp.Core.Parsing;
using VcrSharp.Core.Parsing.Ast;

namespace VcrSharp.Core.Tests.Parsing.TapeParserTests;

/// <summary>
/// Tests for escape sequence handling in quoted strings.
/// </summary>
public class EscapeSequenceTests
{
    [Fact]
    public void ParseTape_DoubleQuotedStringWithNewline_ParsesEscapeSequence()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Type \"Line 1\\nLine 2\"";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<TypeCommand>();
        cmd.Text.ShouldBe("Line 1\nLine 2");
    }

    [Fact]
    public void ParseTape_DoubleQuotedStringWithTab_ParsesEscapeSequence()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Type \"Name\\tAge\\tCity\"";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<TypeCommand>();
        cmd.Text.ShouldBe("Name\tAge\tCity");
    }

    [Fact]
    public void ParseTape_DoubleQuotedStringWithCarriageReturn_ParsesEscapeSequence()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Type \"Line 1\\rLine 2\"";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<TypeCommand>();
        cmd.Text.ShouldBe("Line 1\rLine 2");
    }

    [Fact]
    public void ParseTape_DoubleQuotedStringWithEscapedBackslash_ParsesEscapeSequence()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Type \"C:\\\\\\\\Users\\\\\\\\Name\"";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<TypeCommand>();
        cmd.Text.ShouldBe("C:\\\\Users\\\\Name");
    }

    [Fact]
    public void ParseTape_DoubleQuotedStringWithEscapedDoubleQuote_ParsesEscapeSequence()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Type \"This is \\\"my story's end\\\"\"";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<TypeCommand>();
        cmd.Text.ShouldBe("This is \"my story's end\"");
    }

    [Fact]
    public void ParseTape_DoubleQuotedStringWithUnknownEscape_PreservesBackslash()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Type \"C:\\Users\\Name\"";

        // Act
        var commands = parser.ParseTape(source);

        // Assert (backward compatibility - unknown escapes preserved)
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<TypeCommand>();
        cmd.Text.ShouldBe("C:\\Users\\Name");
    }

    [Fact]
    public void ParseTape_DoubleQuotedStringWithMultipleEscapes_ParsesAllCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Type \"Line 1\\nLine 2\\tTabbed\\nLine 3\"";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<TypeCommand>();
        cmd.Text.ShouldBe("Line 1\nLine 2\tTabbed\nLine 3");
    }

    [Fact]
    public void ParseTape_SingleQuotedStringWithBackslash_NoEscapeProcessing()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Type 'C:\\Users\\Name\\ntest'";

        // Act
        var commands = parser.ParseTape(source);

        // Assert (single quotes remain literal - no escape processing)
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<TypeCommand>();
        cmd.Text.ShouldBe("C:\\Users\\Name\\ntest");
    }

    [Fact]
    public void ParseTape_BacktickQuotedStringWithBackslash_NoEscapeProcessing()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Type `C:\\Users\\Name\\ntest`";

        // Act
        var commands = parser.ParseTape(source);

        // Assert (backticks remain literal - no escape processing)
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<TypeCommand>();
        cmd.Text.ShouldBe("C:\\Users\\Name\\ntest");
    }

    [Fact]
    public void ParseTape_CopyCommandWithEscapedQuotes_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Copy \"Say \\\"Hello\\\" to the world\"";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<CopyCommand>();
        cmd.Text.ShouldBe("Say \"Hello\" to the world");
    }

    [Fact]
    public void ParseTape_SetCommandWithEscapedBackslash_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Set WorkingDirectory \"C:\\\\\\\\Projects\\\\\\\\MyApp\"";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<SetCommand>();
        cmd.Value.ShouldBe("C:\\\\Projects\\\\MyApp");
    }

    [Fact]
    public void ParseTape_MixedQuoteStyles_ParsesEachCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Type \"Double with \\n newline\"\n" +
                     "Type 'Single with \\n literal'\n" +
                     "Type `Backtick with \\n literal`";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(3);

        var cmd1 = commands[0].ShouldBeOfType<TypeCommand>();
        cmd1.Text.ShouldBe("Double with \n newline");

        var cmd2 = commands[1].ShouldBeOfType<TypeCommand>();
        cmd2.Text.ShouldBe("Single with \\n literal");

        var cmd3 = commands[2].ShouldBeOfType<TypeCommand>();
        cmd3.Text.ShouldBe("Backtick with \\n literal");
    }

    [Fact]
    public void ParseTape_ExecCommandWithEscapedQuotes_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Exec \"echo \\\"Hello, World!\\\"\"";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<ExecCommand>();
        cmd.Command.ShouldBe("echo \"Hello, World!\"");
    }

    [Fact]
    public void ParseTape_DoubleQuotedStringWithAllEscapes_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Type \"Test: \\n\\t\\r\\\\\\\"\"";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<TypeCommand>();
        cmd.Text.ShouldBe("Test: \n\t\r\\\"");
    }

    [Fact]
    public void ParseTape_EmptyStringWithEscapes_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Type \"\\n\\t\"";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<TypeCommand>();
        cmd.Text.ShouldBe("\n\t");
    }

    [Fact]
    public void ParseTape_ConsecutiveBackslashes_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Type \"\\\\\\\\\\\\\\\\\"";  // Four backslashes

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<TypeCommand>();
        cmd.Text.ShouldBe("\\\\\\\\");  // Results in four backslashes
    }
}
