using Shouldly;
using VcrSharp.Core.Parsing;
using VcrSharp.Core.Parsing.Ast;

namespace VcrSharp.Core.Tests.Parsing.TapeParserTests;

/// <summary>
/// Tests for TapeParser wait command parsing.
/// </summary>
public class WaitCommandTests
{
    [Fact]
    public void ParseTape_WaitCommand_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Wait";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<WaitCommand>();
        cmd.Scope.ShouldBe(WaitScope.Buffer);
        cmd.Timeout.ShouldBeNull();
        cmd.Pattern.ShouldBeNull();
    }

    [Fact]
    public void ParseTape_WaitScreenCommand_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Wait+Screen";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<WaitCommand>();
        cmd.Scope.ShouldBe(WaitScope.Screen);
        cmd.Timeout.ShouldBeNull();
        cmd.Pattern.ShouldBeNull();
    }

    [Fact]
    public void ParseTape_WaitLineCommand_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Wait+Line";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<WaitCommand>();
        cmd.Scope.ShouldBe(WaitScope.Line);
    }

    [Fact]
    public void ParseTape_WaitWithTimeout_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Wait@5s";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<WaitCommand>();
        cmd.Timeout.ShouldNotBeNull();
        cmd.Timeout.Value.TotalSeconds.ShouldBe(5);
    }

    [Fact]
    public void ParseTape_WaitWithPattern_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Wait /pattern/";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<WaitCommand>();
        cmd.Pattern.ShouldNotBeNull();
        cmd.Pattern.ToString().ShouldBe("pattern");
    }

    [Fact]
    public void ParseTape_WaitScreenWithTimeout_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Wait+Screen@2s";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<WaitCommand>();
        cmd.Scope.ShouldBe(WaitScope.Screen);
        cmd.Timeout!.Value.TotalSeconds.ShouldBe(2);
    }

    [Fact]
    public void ParseTape_WaitLineWithTimeoutAndPattern_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Wait+Line@10ms /pattern/";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<WaitCommand>();
        cmd.Scope.ShouldBe(WaitScope.Line);
        cmd.Timeout!.Value.TotalMilliseconds.ShouldBe(10);
        cmd.Pattern!.ToString().ShouldBe("pattern");
    }

    [Fact]
    public void ParseTape_WaitWithMultipleSpacesBeforePattern_ParsesCorrectly()
    {
        // Arrange - regression test for spacing issue
        var parser = new TapeParser();
        var source = "Wait    /pattern/";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<WaitCommand>();
        cmd.Pattern!.ToString().ShouldBe("pattern");
    }

    [Fact]
    public void ParseTape_WaitWithPatternContainingSpaces_ParsesCorrectly()
    {
        // Arrange - regression test for patterns with spaces
        var parser = new TapeParser();
        var source = "Wait /size pizza/";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<WaitCommand>();
        cmd.Pattern!.ToString().ShouldBe("size pizza");
    }

    [Fact]
    public void ParseTape_WaitWithMultipleSpacesAndPatternWithSpaces_ParsesCorrectly()
    {
        // Arrange - regression test for the originally reported bug
        var parser = new TapeParser();
        var source = "Wait    /size pizza/";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<WaitCommand>();
        cmd.Pattern!.ToString().ShouldBe("size pizza");
    }

    [Theory]
    [InlineData("Wait")]
    [InlineData("Wait            /World/")]
    [InlineData("Wait+Screen     /World/")]
    [InlineData("Wait+Line       /World/")]
    [InlineData("Wait@10ms       /World/")]
    [InlineData("Wait+Line@10ms  /World/")]
    public void ParseTape_WaitVariations_ParsesWithoutError(string source)
    {
        // Arrange
        var parser = new TapeParser();

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        commands[0].ShouldBeOfType<WaitCommand>();
    }
}