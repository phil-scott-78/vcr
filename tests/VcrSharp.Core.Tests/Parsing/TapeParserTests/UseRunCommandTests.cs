using Shouldly;
using VcrSharp.Core.Parsing;
using VcrSharp.Core.Parsing.Ast;

namespace VcrSharp.Core.Tests.Parsing.TapeParserTests;

/// <summary>Tests for the Use / Run commands and the Exec macro form.</summary>
public class UseRunCommandTests
{
    private static readonly TapeParser Parser = new();

    [Fact]
    public void ParseTape_Use_ParsesPresetName()
    {
        var cmd = Parser.ParseTape("Use doc").ShouldHaveSingleItem().ShouldBeOfType<UseCommand>();
        cmd.PresetName.ShouldBe("doc");
    }

    [Fact]
    public void ParseTape_UseQuoted_ParsesPresetName()
    {
        Parser.ParseTape("Use \"landing page\"").ShouldHaveSingleItem()
            .ShouldBeOfType<UseCommand>().PresetName.ShouldBe("landing page");
    }

    [Fact]
    public void ParseTape_Run_ParsesText()
    {
        Parser.ParseTape("Run \"./example Alice\"").ShouldHaveSingleItem()
            .ShouldBeOfType<RunCommand>().Text.ShouldBe("./example Alice");
    }

    [Fact]
    public void ParseTape_ExecLiteral_IsNotMacro()
    {
        var exec = Parser.ParseTape("Exec \"dotnet --version\"").ShouldHaveSingleItem().ShouldBeOfType<ExecCommand>();
        exec.IsMacro.ShouldBeFalse();
        exec.Command.ShouldBe("dotnet --version");
    }

    [Fact]
    public void ParseTape_ExecMacroWithArg_IsMacro()
    {
        var exec = Parser.ParseTape("Exec showcase table").ShouldHaveSingleItem().ShouldBeOfType<ExecCommand>();
        exec.IsMacro.ShouldBeTrue();
        exec.Command.ShouldBe("showcase");
        exec.MacroArg.ShouldBe("table");
    }

    [Fact]
    public void ParseTape_ExecMacroNoArg_IsMacroWithNullArg()
    {
        var exec = Parser.ParseTape("Exec showcase").ShouldHaveSingleItem().ShouldBeOfType<ExecCommand>();
        exec.IsMacro.ShouldBeTrue();
        exec.Command.ShouldBe("showcase");
        exec.MacroArg.ShouldBeNull();
    }

    [Fact]
    public void ParseTape_SetAfterUse_IsAllowed()
    {
        var commands = Parser.ParseTape("Use doc\nSet Rows 14\nExec \"x\"");
        commands[0].ShouldBeOfType<UseCommand>();
        commands[1].ShouldBeOfType<SetCommand>();
    }

    [Fact]
    public void ParseTape_SetAfterRun_IsRejected()
    {
        Should.Throw<TapeParseException>(() => Parser.ParseTape("Run \"x\"\nSet Rows 14"));
    }

    [Fact]
    public void IsKnownSetting_RecognizesKnownAndUnknown()
    {
        TapeParser.IsKnownSetting("Cols").ShouldBeTrue();
        TapeParser.IsKnownSetting("transparentbackground").ShouldBeTrue(); // case-insensitive
        TapeParser.IsKnownSetting("Bogus").ShouldBeFalse();
    }
}
