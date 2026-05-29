using Shouldly;
using VcrSharp.Core.Logging;
using VcrSharp.Core.Parsing;
using VcrSharp.Core.Recording;
using VcrSharp.Core.Session;
using VcrSharp.Infrastructure.Playwright;
using VcrSharp.Infrastructure.Processes;

namespace VcrSharp.Integration.Tests;

/// <summary>
/// End-to-end tests for interactive record mode's input-capture hooks. These drive a real ttyd + xterm.js
/// terminal and feed keystrokes via Playwright (which exercises the same xterm <c>onData</c> path a human
/// would), then assert the captured events convert into valid, replayable tape.
/// </summary>
public class InteractiveRecordTests : IAsyncDisposable
{
    private PlaywrightBrowser? _browser;
    private TtydProcess? _ttyd;

    [Fact]
    public async Task CaptureHooks_RoundTrip_ProduceValidReplayableTape()
    {
        var ct = TestContext.Current.CancellationToken;
        VcrLogger.Configure(verbose: false);

        // Start an interactive shell via ttyd using the platform default shell.
        var shellConfig = ShellConfiguration.GetConfiguration(new SessionOptions().Shell);
        _ttyd = new TtydProcess(shellConfig.BuildTtydCommand(), shellConfig, execCommands: []);
        await _ttyd.StartAsync();

        // Headless is fine here: Playwright keyboard events reach the focused xterm helper textarea
        // and trigger onData exactly as real typing does.
        _browser = new PlaywrightBrowser();
        await _browser.LaunchAsync(headless: true);
        var page = await _browser.NewPageAsync($"http://localhost:{_ttyd.Port}", width: 1200, height: 600);
        var terminal = new TerminalPage(page);

        await terminal.WaitForTerminalReadyAsync();
        await terminal.ClickTerminalAsync();
        await terminal.WaitForBufferContentAsync();

        // Begin capture, then "type" as a user would.
        await terminal.StartInputCaptureAsync();
        await terminal.TypeAsync("echo hi", delayMs: 20);
        await terminal.PressKeyAsync("Enter");
        await Task.Delay(300, ct); // let onData flush

        var events = await terminal.DrainInputCaptureAsync();
        events.ShouldNotBeEmpty();

        // Convert and assert the tape reproduces the session. StripExit disabled — no exit was typed.
        var tape = InputToTapeConverter.Convert(events, new InputToTapeOptions { StripExit = false });
        tape.ShouldContain("Type \"echo hi\"");
        tape.ShouldContain("Enter");

        // The generated tape must be valid for playback.
        var commands = new TapeParser().ParseTape(tape);
        commands.ShouldNotBeEmpty();
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser != null)
        {
            await _browser.CloseAsync();
        }
        _ttyd?.Dispose();
        GC.SuppressFinalize(this);
    }
}
