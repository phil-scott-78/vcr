using Shouldly;
using VcrSharp.Core.Parsing;
using VcrSharp.Core.Parsing.Ast;
using VcrSharp.Core.Session;
using VcrSharp.Infrastructure.Session;

namespace VcrSharp.Core.Tests.Session;

/// <summary>
/// Guards the native (browserless) launch shape. The rule: a tape with <c>Exec</c> launches it as the
/// shell's hidden foreground process — NEVER typed into a REPL — so the launch line never leaks into the
/// recording. Regression test for the "launch the app, then drive it" case (mixed Exec + Type/Key tapes,
/// e.g. an interactive-prompt demo), where the native path used to echo the <c>dotnet run …</c> launch line.
/// </summary>
public class NativeLaunchTests
{
    private static readonly TapeParser Parser = new();

    [Fact]
    public void ShouldUseBareRepl_IsTrue_OnlyWhenNoExec()
    {
        // Pure interactive: types its own command into a live REPL — the command line IS the demo, must show.
        NativeRecordingSession.ShouldUseBareRepl(Parser.ParseTape("Type \"ls\"\nEnter"))
            .ShouldBeTrue();

        // Pure showcase: Exec launched as a hidden foreground process.
        NativeRecordingSession.ShouldUseBareRepl(Parser.ParseTape("Exec \"ls\""))
            .ShouldBeFalse();

        // Mixed — the bug case: launch the app via Exec, then answer its prompts with Type/Key. Must NOT be a
        // REPL (else the launch line gets typed and echoed).
        NativeRecordingSession.ShouldUseBareRepl(Parser.ParseTape("Exec \"myapp\"\nType \"Teddy\"\nEnter"))
            .ShouldBeFalse();
    }

    [Fact]
    public void BuildUnixArgv_WithExec_LaunchesForeground_NotTypedIntoRepl()
    {
        var commands = Parser.ParseTape("Exec \"myapp --flag\"\nType \"hi\"\nEnter");
        var execs = commands.OfType<ExecCommand>().ToList();
        var config = ShellConfiguration.GetConfiguration("bash");

        var argv = NativeRecordingSession.BuildUnixArgv(config, bareRepl: false, execs);

        // The launch command is an ARGUMENT to the shell (foreground), not keystrokes typed at a prompt.
        argv.ShouldBe(new[] { config.Name, config.ExecutionFlag, "myapp --flag" });
    }

    [Fact]
    public void BuildUnixArgv_NoExec_UsesInteractiveReplInvocation()
    {
        var config = ShellConfiguration.GetConfiguration("bash");

        var argv = NativeRecordingSession.BuildUnixArgv(config, bareRepl: true, new List<ExecCommand>());

        argv.ShouldBe(config.BuildTtydCommand());
    }
}
