using Shouldly;
using VcrSharp.Core.Session;
using ExecutionContext = VcrSharp.Core.Parsing.Ast.ExecutionContext;
using ScreenshotCommand = VcrSharp.Core.Parsing.Ast.ScreenshotCommand;

namespace VcrSharp.Core.Tests.Parsing.Ast.CommandTests;

/// <summary>
/// Tests for ScreenshotCommand execution: the optional wait-for-settle (Theme B MVP).
/// </summary>
public class ScreenshotCommandExecutionTests
{
    private sealed class FakeFrameCapture : IFrameCapture
    {
        public readonly List<string> Calls = new();

        public Task CaptureScreenshotAsync(string filePath)
        {
            Calls.Add("capture");
            return Task.CompletedTask;
        }

        public Task WaitForBufferStableAsync(TimeSpan inactivityTimeout, TimeSpan maxWait, CancellationToken cancellationToken = default)
        {
            Calls.Add("wait");
            return Task.CompletedTask;
        }
    }

    private static ExecutionContext Context(SessionOptions options, FakeFrameCapture fc) =>
        new(options, new SessionState(), null!, fc);

    [Fact]
    public async Task Execute_WhenWaitEnabled_SettlesThenCaptures()
    {
        var fc = new FakeFrameCapture();
        var options = new SessionOptions { ScreenshotWaitForInactivity = true };

        await new ScreenshotCommand("out.svg").ExecuteAsync(Context(options, fc), TestContext.Current.CancellationToken);

        fc.Calls.ShouldBe(new[] { "wait", "capture" });
    }

    [Fact]
    public async Task Execute_WhenWaitDisabled_CapturesImmediately()
    {
        var fc = new FakeFrameCapture();
        var options = new SessionOptions { ScreenshotWaitForInactivity = false };

        await new ScreenshotCommand("out.svg").ExecuteAsync(Context(options, fc), TestContext.Current.CancellationToken);

        fc.Calls.ShouldBe(new[] { "capture" });
    }
}
