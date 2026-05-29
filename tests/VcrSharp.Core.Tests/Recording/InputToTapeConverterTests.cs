using Shouldly;
using VcrSharp.Core.Parsing;
using VcrSharp.Core.Parsing.Ast;
using VcrSharp.Core.Recording;
using VcrSharp.Core.Session;
using VcrSharp.Core.Settings;

namespace VcrSharp.Core.Tests.Recording;

/// <summary>
/// Tests for <see cref="InputToTapeConverter"/>. Each test asserts on the generated tape text and,
/// crucially, re-parses the output through <see cref="TapeParser"/> to guarantee the converter only
/// ever emits parser-valid tape.
/// </summary>
public class InputToTapeConverterTests
{
    // ESC (0x1b). Written as a separate literal so the \x escape is terminated by the quote and
    // cannot absorb a following hex-digit character (e.g. ESC + 'b').
    private const string Esc = "\x1b";

    private static InputEvent Ev(string data, double ms) => new(data, TimeSpan.FromMilliseconds(ms));

    private static string Convert(params InputEvent[] events) =>
        InputToTapeConverter.Convert(events);

    private static string Convert(InputToTapeOptions options, params InputEvent[] events) =>
        InputToTapeConverter.Convert(events, options);

    /// <summary>Re-parses generated tape to prove it is syntactically valid.</summary>
    private static List<ICommand> Reparse(string tape) => new TapeParser().ParseTape(tape);

    [Fact]
    public void PlainTyping_Coalesces_IntoSingleType()
    {
        var tape = Convert(Ev("h", 0), Ev("i", 80));

        tape.ShouldBe("Type \"hi\"");
        var cmd = Reparse(tape).ShouldHaveSingleItem().ShouldBeOfType<TypeCommand>();
        cmd.Text.ShouldBe("hi");
    }

    [Fact]
    public void Typing_ThenEnter()
    {
        var tape = Convert(Ev("l", 0), Ev("s", 60), Ev("\r", 120));

        tape.ShouldBe("Type \"ls\"\nEnter");
        var cmds = Reparse(tape);
        cmds.Count.ShouldBe(2);
        cmds[0].ShouldBeOfType<TypeCommand>().Text.ShouldBe("ls");
        cmds[1].ShouldBeOfType<KeyCommand>().KeyName.ShouldBe("Enter");
    }

    [Fact]
    public void RepeatedArrows_AreGrouped()
    {
        var tape = Convert(Ev(Esc + "[A", 0), Ev(Esc + "[A", 50), Ev(Esc + "[A", 100));

        tape.ShouldBe("Up 3");
        var cmd = Reparse(tape).ShouldHaveSingleItem().ShouldBeOfType<KeyCommand>();
        cmd.KeyName.ShouldBe("Up");
        cmd.RepeatCount.ShouldBe(3);
    }

    [Fact]
    public void Arrows_BrokenByGap_AreNotGrouped()
    {
        var tape = Convert(Ev(Esc + "[A", 0), Ev(Esc + "[A", 400));

        tape.ShouldBe("Up\nSleep 400ms\nUp");
        var cmds = Reparse(tape);
        cmds.Count.ShouldBe(3);
        cmds[0].ShouldBeOfType<KeyCommand>().KeyName.ShouldBe("Up");
        cmds[1].ShouldBeOfType<SleepCommand>();
        cmds[2].ShouldBeOfType<KeyCommand>().KeyName.ShouldBe("Up");
    }

    [Fact]
    public void CtrlC_MapsToModifier()
    {
        var tape = Convert(Ev("\x03", 0));

        tape.ShouldBe("Ctrl+C");
        var cmd = Reparse(tape).ShouldHaveSingleItem().ShouldBeOfType<ModifierCommand>();
        cmd.HasCtrl.ShouldBeTrue();
        cmd.Key.ShouldBe("C");
    }

    [Fact]
    public void RepeatedModifiers_AreNotGrouped()
    {
        var tape = Convert(Ev("\x03", 0), Ev("\x03", 50));

        tape.ShouldBe("Ctrl+C\nCtrl+C");
        Reparse(tape).Count.ShouldBe(2);
    }

    [Fact]
    public void Sleep_IsInserted_FromTimestamps()
    {
        var tape = Convert(Ev("a", 0), Ev("b", 500));

        tape.ShouldBe("Type \"a\"\nSleep 500ms\nType \"b\"");
        var cmds = Reparse(tape);
        cmds.Count.ShouldBe(3);
        cmds[1].ShouldBeOfType<SleepCommand>().Duration.ShouldBe(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void Sleep_RoundsToSeconds_ForLongGaps()
    {
        var tape = Convert(Ev("a", 0), Ev("b", 1480));

        tape.ShouldBe("Type \"a\"\nSleep 1.5s\nType \"b\"");
        Reparse(tape)[1].ShouldBeOfType<SleepCommand>().Duration.ShouldBe(TimeSpan.FromSeconds(1.5));
    }

    [Fact]
    public void Sleep_RoundsToNearest50ms_ForShortGaps()
    {
        var tape = Convert(Ev("a", 0), Ev("b", 340));

        tape.ShouldBe("Type \"a\"\nSleep 350ms\nType \"b\"");
    }

    [Fact]
    public void SubThresholdGap_ProducesNoSleep()
    {
        var tape = Convert(Ev("a", 0), Ev("b", 120));

        tape.ShouldBe("Type \"ab\"");
    }

    [Fact]
    public void Quoting_EscapesEmbeddedQuotes()
    {
        // Type: say "hi"
        var tape = Convert(
            Ev("s", 0), Ev("a", 10), Ev("y", 20), Ev(" ", 30),
            Ev("\"", 40), Ev("h", 50), Ev("i", 60), Ev("\"", 70));

        tape.ShouldBe("Type \"say \\\"hi\\\"\"");
        Reparse(tape).ShouldHaveSingleItem().ShouldBeOfType<TypeCommand>().Text.ShouldBe("say \"hi\"");
    }

    [Fact]
    public void Quoting_EscapesBackslash()
    {
        // Type: C:\x
        var tape = Convert(Ev("C", 0), Ev(":", 10), Ev("\\", 20), Ev("x", 30));

        tape.ShouldBe("Type \"C:\\\\x\"");
        Reparse(tape).ShouldHaveSingleItem().ShouldBeOfType<TypeCommand>().Text.ShouldBe("C:\\x");
    }

    [Fact]
    public void Backspace_MapsFromBothBytes()
    {
        Convert(Ev("\x7f", 0)).ShouldBe("Backspace");
        Convert(Ev("\x08", 0)).ShouldBe("Backspace");
    }

    [Fact]
    public void Tab_And_Enter_AreKeys_NotControlCombos()
    {
        Convert(Ev("\t", 0)).ShouldBe("Tab");
        Convert(Ev("\r", 0)).ShouldBe("Enter");
    }

    [Fact]
    public void LoneEscape_MapsToEscape()
    {
        var tape = Convert(Ev(Esc, 0));

        tape.ShouldBe("Escape");
        Reparse(tape).ShouldHaveSingleItem().ShouldBeOfType<KeyCommand>().KeyName.ShouldBe("Escape");
    }

    [Fact]
    public void EscapeSequence_IsNotConfusedWithEscape()
    {
        Convert(Ev(Esc + "[D", 0)).ShouldBe("Left");
    }

    [Fact]
    public void SplitEscapeSequence_IsReassembled()
    {
        // ESC arrives in one event, "[A" in the next — concatenation reassembles into Up.
        var tape = Convert(Ev(Esc, 0), Ev("[A", 10));

        tape.ShouldBe("Up");
    }

    [Fact]
    public void Ss3Arrow_MapsToKey()
    {
        Convert(Ev(Esc + "OA", 0)).ShouldBe("Up");
    }

    [Fact]
    public void AltChar_FromSameEvent_MapsToAltModifier()
    {
        // ESC + 'b' delivered as a single event is the meta/Alt prefix.
        var tape = Convert(Ev(Esc + "b", 0));

        tape.ShouldBe("Alt+b");
        var cmd = Reparse(tape).ShouldHaveSingleItem().ShouldBeOfType<ModifierCommand>();
        cmd.HasAlt.ShouldBeTrue();
        cmd.Key.ShouldBe("b");
    }

    [Fact]
    public void ModifiedArrows_DecodeModifiers()
    {
        Convert(Ev(Esc + "[1;5C", 0)).ShouldBe("Ctrl+Right");
        Convert(Ev(Esc + "[1;2A", 0)).ShouldBe("Shift+Up");

        Reparse("Ctrl+Right").ShouldHaveSingleItem().ShouldBeOfType<ModifierCommand>().Key.ShouldBe("Right");
        Reparse("Shift+Up").ShouldHaveSingleItem().ShouldBeOfType<ModifierCommand>().Key.ShouldBe("Up");
    }

    [Fact]
    public void NavigationKeys_Map()
    {
        Convert(Ev(Esc + "[3~", 0)).ShouldBe("Delete");
        Convert(Ev(Esc + "[H", 0)).ShouldBe("Home");
        Convert(Ev(Esc + "[6~", 0)).ShouldBe("PageDown");
        Convert(Ev(Esc + "[Z", 0)).ShouldBe("Shift+Tab");

        Reparse("Shift+Tab").ShouldHaveSingleItem().ShouldBeOfType<ModifierCommand>().Key.ShouldBe("Tab");
    }

    [Fact]
    public void TrailingExit_IsStripped_ByDefault()
    {
        // "exit" + Enter
        var tape = Convert(
            Ev("e", 0), Ev("x", 10), Ev("i", 20), Ev("t", 30), Ev("\r", 40));

        tape.ShouldBe(string.Empty);
        Reparse(tape).ShouldBeEmpty();
    }

    [Fact]
    public void TrailingExit_IsRetained_WhenStripExitDisabled()
    {
        var options = new InputToTapeOptions { StripExit = false };
        var tape = Convert(options,
            Ev("e", 0), Ev("x", 10), Ev("i", 20), Ev("t", 30), Ev("\r", 40));

        tape.ShouldBe("Type \"exit\"\nEnter");
    }

    [Fact]
    public void TrailingExit_IsCaseInsensitive_AndTrimmed()
    {
        var tape = Convert(
            Ev(" ", 0), Ev("E", 10), Ev("X", 20), Ev("I", 30), Ev("T", 40), Ev(" ", 50), Ev("\r", 60));

        tape.ShouldBe(string.Empty);
    }

    [Fact]
    public void CtrlD_TerminatesSession_AndIsStripped()
    {
        var tape = Convert(Ev("l", 0), Ev("s", 10), Ev("\r", 20), Ev("\x04", 30));

        tape.ShouldBe("Type \"ls\"\nEnter");
    }

    [Fact]
    public void BracketedPaste_BecomesType()
    {
        var tape = Convert(Ev(Esc + "[200~git status" + Esc + "[201~", 0));

        tape.ShouldBe("Type \"git status\"");
        Reparse(tape).ShouldHaveSingleItem().ShouldBeOfType<TypeCommand>().Text.ShouldBe("git status");
    }

    [Fact]
    public void BracketedPaste_EscapesSpecialCharacters()
    {
        var tape = Convert(Ev(Esc + "[200~a\tb\"c\\d" + Esc + "[201~", 0));

        tape.ShouldBe("Type \"a\\tb\\\"c\\\\d\"");
        Reparse(tape).ShouldHaveSingleItem().ShouldBeOfType<TypeCommand>().Text.ShouldBe("a\tb\"c\\d");
    }

    [Fact]
    public void Header_EmitsNonDefaultShell()
    {
        var options = new InputToTapeOptions { Shell = "pwsh", DefaultShell = "bash" };
        var tape = InputToTapeConverter.Convert([], options);

        tape.ShouldBe("Set Shell \"pwsh\"");
        Reparse(tape).ShouldHaveSingleItem().ShouldBeOfType<SetCommand>().SettingName.ShouldBe("Shell");
    }

    [Fact]
    public void Header_OmitsShell_WhenMatchingDefault()
    {
        var options = new InputToTapeOptions { Shell = "bash", DefaultShell = "bash" };

        InputToTapeConverter.Convert([], options).ShouldBe(string.Empty);
    }

    [Fact]
    public void Header_EmitsConfiguredSessionSettings()
    {
        var header = new SessionOptions { Cols = 80, Rows = 24, Theme = BuiltinThemes.Dracula };
        var options = new InputToTapeOptions { Header = header };

        var tape = InputToTapeConverter.Convert([], options);

        tape.ShouldBe("Set Cols 80\nSet Rows 24\nSet Theme \"Dracula\"");
        var cmds = Reparse(tape);
        cmds.Count.ShouldBe(3);
        cmds.ShouldAllBe(c => c is SetCommand);
    }

    [Fact]
    public void EmptyInput_ProducesEmptyTape()
    {
        var tape = InputToTapeConverter.Convert([]);

        tape.ShouldBe(string.Empty);
        Reparse(tape).ShouldBeEmpty();
    }

    [Fact]
    public void FullSession_ProducesValidTape()
    {
        var header = new SessionOptions { Cols = 100 };
        var options = new InputToTapeOptions { Shell = "pwsh", DefaultShell = "bash", Header = header };

        var tape = InputToTapeConverter.Convert(
        [
            Ev("l", 0), Ev("s", 60), Ev("\r", 120),
            Ev(Esc + "[A", 2120), Ev(Esc + "[A", 2170),
            Ev("\x03", 2220),
            // session-ending exit (stripped)
            Ev("e", 2280), Ev("x", 2290), Ev("i", 2300), Ev("t", 2310), Ev("\r", 2400)
        ], options);

        tape.ShouldBe(
            "Set Shell \"pwsh\"\n" +
            "Set Cols 100\n" +
            "\n" +
            "Type \"ls\"\n" +
            "Enter\n" +
            "Sleep 2s\n" +
            "Up 2\n" +
            "Ctrl+C");

        // The whole thing must be valid, replayable tape.
        var cmds = Reparse(tape);
        cmds.OfType<SetCommand>().Count().ShouldBe(2);
        cmds.OfType<TypeCommand>().ShouldHaveSingleItem().Text.ShouldBe("ls");
        cmds.OfType<SleepCommand>().ShouldHaveSingleItem();
        cmds.OfType<ModifierCommand>().ShouldHaveSingleItem().HasCtrl.ShouldBeTrue();
    }
}
