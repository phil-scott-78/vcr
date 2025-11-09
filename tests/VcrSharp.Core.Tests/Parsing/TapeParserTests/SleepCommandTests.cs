using Shouldly;
using VcrSharp.Core.Parsing;
using VcrSharp.Core.Parsing.Ast;

namespace VcrSharp.Core.Tests.Parsing.TapeParserTests;

/// <summary>
/// Tests for TapeParser sleep command parsing.
/// </summary>
public class SleepCommandTests
{
    [Fact]
    public void ParseTape_SleepWithSeconds_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Sleep 1s";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<SleepCommand>();
        cmd.Duration.TotalSeconds.ShouldBe(1);
    }

    [Fact]
    public void ParseTape_SleepWithMilliseconds_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Sleep 500ms";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<SleepCommand>();
        cmd.Duration.TotalMilliseconds.ShouldBe(500);
    }

    [Fact]
    public void ParseTape_SleepWithMinutes_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Sleep 2m";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<SleepCommand>();
        cmd.Duration.TotalMinutes.ShouldBe(2);
    }

    [Fact]
    public void ParseTape_SleepWithBareNumber_ParsesAsSeconds()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Sleep 3";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<SleepCommand>();
        cmd.Duration.TotalSeconds.ShouldBe(3);
    }

    [Fact]
    public void ParseTape_SleepWithDecimal_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Sleep 1.5s";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<SleepCommand>();
        cmd.Duration.TotalSeconds.ShouldBe(1.5);
    }

    [Fact]
    public void ParseTape_SleepWithDecimalBareNumber_ParsesCorrectly()
    {
        // Arrange
        var parser = new TapeParser();
        var source = "Sleep 2.5";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(1);
        var cmd = commands[0].ShouldBeOfType<SleepCommand>();
        cmd.Duration.TotalSeconds.ShouldBe(2.5);
    }

    [Fact]
    public void ParseTape_MultipleSleepCommands_ParsesAll()
    {
        // Arrange
        var parser = new TapeParser();
        var source = @"Sleep 1s
Sleep 500ms
Sleep 2";

        // Act
        var commands = parser.ParseTape(source);

        // Assert
        commands.Count.ShouldBe(3);

        var cmd1 = commands[0].ShouldBeOfType<SleepCommand>();
        cmd1.Duration.TotalSeconds.ShouldBe(1);

        var cmd2 = commands[1].ShouldBeOfType<SleepCommand>();
        cmd2.Duration.TotalMilliseconds.ShouldBe(500);

        var cmd3 = commands[2].ShouldBeOfType<SleepCommand>();
        cmd3.Duration.TotalSeconds.ShouldBe(2);
    }

    [Fact]
    public async Task SleepCommand_ExecuteAsync_DelaysCorrectly()
    {
        // Arrange
        var sleepDuration = TimeSpan.FromMilliseconds(100);
        var command = new SleepCommand(sleepDuration);
        var context = new VcrSharp.Core.Parsing.Ast.ExecutionContext(new Session.SessionOptions(), new Session.SessionState());
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        await command.ExecuteAsync(context, TestContext.Current.CancellationToken);
        stopwatch.Stop();

        // Assert
        // Allow 50ms tolerance for timing variations
        stopwatch.ElapsedMilliseconds.ShouldBeInRange(50, 200);
    }

    [Fact]
    public async Task SleepCommand_ExecuteAsync_RespectsCancellation()
    {
        // Arrange
        var sleepDuration = TimeSpan.FromSeconds(10);
        var command = new SleepCommand(sleepDuration);
        var context = new VcrSharp.Core.Parsing.Ast.ExecutionContext(new Session.SessionOptions(), new Session.SessionState());
        var cts = new CancellationTokenSource();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var task = command.ExecuteAsync(context, cts.Token);
        await Task.Delay(50, TestContext.Current.CancellationToken); // Let sleep start
        cts.Cancel();

        // Assert
        await Should.ThrowAsync<OperationCanceledException>(async () => await task);
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(1000); // Should cancel quickly
    }

    [Fact]
    public void SleepCommand_ToString_FormatsCorrectly()
    {
        // Arrange
        var command = new SleepCommand(TimeSpan.FromSeconds(2.5));

        // Act
        var result = command.ToString();

        // Assert
        result.ShouldBe("Sleep 2.5s");
    }
}