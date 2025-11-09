using Shouldly;
using VcrSharp.Core.Parsing;

namespace VcrSharp.Core.Tests.Parsing.TapeParserTests;

/// <summary>
/// Tests for TapeParser error cases.
/// </summary>
public class ErrorCaseTests
{
    [Fact]
    public void ParseTape_MissingRequiredParameter_ThrowsException()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Type"; // Missing string parameter

        // Act & Assert
        Should.Throw<TapeParseException>(() => parser.ParseTape(source));
    }

    [Fact]
    public void ParseTape_InvalidCommand_ThrowsException()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "InvalidCommand";

        // Act & Assert
        Should.Throw<TapeParseException>(() => parser.ParseTape(source));
    }

    [Fact]
    public void ParseTape_MalformedString_ThrowsException()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Type \"unterminated";

        // Act & Assert
        Should.Throw<TapeParseException>(() => parser.ParseTape(source));
    }

    [Fact]
    public void ParseTape_InvalidDuration_ThrowsException()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Sleep abc";

        // Act & Assert
        Should.Throw<TapeParseException>(() => parser.ParseTape(source));
    }

    [Fact]
    public void ParseTape_InvalidRegexPattern_ThrowsException()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Wait /[unclosed/";

        // Act & Assert
        Should.Throw<Exception>(() => parser.ParseTape(source));
    }

    [Fact]
    public void ParseTape_SetWithoutSettingName_ThrowsException()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Set";

        // Act & Assert
        Should.Throw<TapeParseException>(() => parser.ParseTape(source));
    }

    [Fact]
    public void ParseTape_OutputWithoutPath_ThrowsException()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Output";

        // Act & Assert
        Should.Throw<TapeParseException>(() => parser.ParseTape(source));
    }

    [Fact]
    public void ParseTape_RequireWithoutProgram_ThrowsException()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Require";

        // Act & Assert
        Should.Throw<TapeParseException>(() => parser.ParseTape(source));
    }

    [Fact]
    public void ParseTape_ExecWithoutCommand_ThrowsException()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Exec";

        // Act & Assert
        Should.Throw<TapeParseException>(() => parser.ParseTape(source));
    }

    [Fact]
    public void ParseTape_CopyWithoutText_ThrowsException()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Copy";

        // Act & Assert
        Should.Throw<TapeParseException>(() => parser.ParseTape(source));
    }

    [Fact]
    public void ParseTape_EnvWithoutValue_ThrowsException()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Env KEY";

        // Act & Assert
        Should.Throw<TapeParseException>(() => parser.ParseTape(source));
    }

    [Fact]
    public void ParseTape_ScreenshotWithoutPath_ThrowsException()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Screenshot";

        // Act & Assert
        Should.Throw<TapeParseException>(() => parser.ParseTape(source));
    }
}