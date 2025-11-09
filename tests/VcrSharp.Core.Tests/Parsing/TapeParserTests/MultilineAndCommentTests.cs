using Shouldly;
using VcrSharp.Core.Parsing;
using VcrSharp.Core.Parsing.Ast;

namespace VcrSharp.Core.Tests.Parsing.TapeParserTests;

/// <summary>
/// Tests for TapeParser multi-line and comment parsing.
/// </summary>
public class MultilineAndCommentTests
{
    [Fact]
    public void ParseTape_MultipleLines_ParsesAllCommands()
    {
        // Arrange
        var parser = new TapeParser();
        var source = @"Type ""line1""
Enter
Type ""line2""";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(3);
        commands[0].ShouldBeOfType<TypeCommand>();
        commands[1].ShouldBeOfType<KeyCommand>();
        commands[2].ShouldBeOfType<TypeCommand>();
    }

    [Fact]
    public void ParseTape_CommentsAreIgnored_ParsesCommandsOnly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = @"# This is a comment
Type ""hello""
# Another comment
Enter";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(2);
        commands[0].ShouldBeOfType<TypeCommand>();
        commands[1].ShouldBeOfType<KeyCommand>();
    }

    [Fact]
    public void ParseTape_EmptyLines_AreIgnored()
    {
        // Arrange
        var parser = new TapeParser();
        var source = @"Type ""hello""

Enter

";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(2);
    }
}