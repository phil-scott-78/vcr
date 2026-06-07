using Shouldly;
using VcrSharp.Core.Config;
using VcrSharp.Core.Parsing;
using VcrSharp.Core.Parsing.Ast;
using VcrSharp.Core.Session;
using VcrSharp.Core.Settings;

namespace VcrSharp.Core.Tests.Config;

/// <summary>Tests for <see cref="PresetResolver"/> expansion of Use / macro / Run / output.</summary>
public class PresetResolverTests
{
    private static readonly TapeParser Parser = new();

    private static List<ICommand> Resolve(string tape, VcrConfig? config, string tapePath = "table.tape")
        => PresetResolver.Resolve(Parser.ParseTape(tape), tapePath, config);

    private static VcrConfig Config(string toml) => VcrConfigReader.Parse(toml, "vcr.toml");

    [Fact]
    public void Resolve_NoUseNoMacroNoRun_ReturnsInputUnchanged()
    {
        var commands = Parser.ParseTape("Set Cols 80\nType \"hi\"\nEnter");
        var resolved = PresetResolver.Resolve(commands, "x.tape", config: null);
        resolved.ShouldBeSameAs(commands);
    }

    [Fact]
    public void Resolve_Use_AppliesPresetSettings()
    {
        var config = Config("""
            [preset.doc]
            theme = "Dracula"
            cols = 82
            rows = 10
            transparentBackground = true
            endBuffer = 5s
            """);

        var options = SessionOptions.FromCommands(Resolve("Use doc\nExec \"x\"", config));

        options.Cols.ShouldBe(82);
        options.Rows.ShouldBe(10);
        options.TransparentBackground.ShouldBeTrue();
        options.EndBuffer.ShouldBe(TimeSpan.FromSeconds(5));
        options.Theme.Name.ShouldBe(BuiltinThemes.GetByName("Dracula")!.Name);
    }

    [Fact]
    public void Resolve_TapeSet_OverridesPreset()
    {
        var config = Config("""
            [preset.doc]
            cols = 82
            rows = 10
            """);

        var resolved = Resolve("Use doc\nSet Rows 14\nExec \"x\"", config);

        // Exactly one Set per key (preset Rows dropped in favor of the tape's).
        resolved.OfType<SetCommand>().Count(s => s.SettingName.Equals("Rows", StringComparison.OrdinalIgnoreCase)).ShouldBe(1);

        var options = SessionOptions.FromCommands(resolved);
        options.Cols.ShouldBe(82);
        options.Rows.ShouldBe(14);
    }

    [Fact]
    public void Resolve_Inherits_BaseFirstThenDerivedWins()
    {
        var config = Config("""
            [preset.doc]
            fontSize = 22
            cols = 82
            [preset.landing]
            inherits = "doc"
            fontSize = 13
            """);

        var options = SessionOptions.FromCommands(Resolve("Use landing\nExec \"x\"", config));
        options.FontSize.ShouldBe(13); // derived overrides base
        options.Cols.ShouldBe(82);     // inherited from base
    }

    [Fact]
    public void Resolve_DerivesOutputFromTapeName_WhenPresetHasOutDirAndNoExplicitOutput()
    {
        var config = Config("""
            [preset.doc]
            outDir = "Spectre.Docs/Content/assets"
            cols = 82
            """);

        var options = SessionOptions.FromCommands(Resolve("Use doc\nExec \"x\"", config, "tree.tape"));
        options.OutputFiles.ShouldContain("Spectre.Docs/Content/assets/tree.svg");
    }

    [Fact]
    public void Resolve_DoesNotDeriveOutput_WhenTapeHasExplicitOutput()
    {
        var config = Config("""
            [preset.doc]
            outDir = "assets"
            """);

        var options = SessionOptions.FromCommands(Resolve("Output \"custom/name.svg\"\nUse doc\nExec \"x\"", config, "tree.tape"));
        options.OutputFiles.ShouldBe(new[] { "custom/name.svg" });
    }

    [Fact]
    public void Resolve_MacroExec_ExpandsToLiteralWithArg()
    {
        var config = Config("""
            [preset.doc]
            cols = 82
            [macro]
            showcase = "dotnet run --no-build showcase {0}"
            """);

        var resolved = Resolve("Use doc\nExec showcase table", config);
        var exec = resolved.OfType<ExecCommand>().ShouldHaveSingleItem();
        exec.IsMacro.ShouldBeFalse();
        exec.Command.ShouldBe("dotnet run --no-build showcase table");
    }

    [Fact]
    public void Resolve_MacroExec_DefaultsArgToTapeBasename()
    {
        var config = Config("""
            [macro]
            showcase = "dotnet run --no-build showcase {0}"
            """);

        var resolved = Resolve("Exec showcase", config, "panel.tape");
        resolved.OfType<ExecCommand>().ShouldHaveSingleItem().Command.ShouldBe("dotnet run --no-build showcase panel");
    }

    [Fact]
    public void Resolve_Run_DesugarsToTypeEnterWait()
    {
        var resolved = Resolve("Run \"./example Alice\"", config: null);

        resolved.Count.ShouldBe(3);
        var type = resolved[0].ShouldBeOfType<TypeCommand>();
        type.Text.ShouldBe("./example Alice");
        resolved[1].ShouldBeOfType<KeyCommand>().KeyName.ShouldBe("Enter");
        resolved[2].ShouldBeOfType<WaitCommand>().Scope.ShouldBe(WaitScope.Buffer);
    }

    [Fact]
    public void Resolve_UnknownPreset_Throws()
    {
        var config = Config("[preset.doc]\ncols = 80");
        var ex = Should.Throw<VcrConfigException>(() => Resolve("Use nope\nExec \"x\"", config));
        ex.Message.ShouldContain("nope");
    }

    [Fact]
    public void Resolve_UnknownMacro_Throws()
    {
        var config = Config("[preset.doc]\ncols = 80");
        Should.Throw<VcrConfigException>(() => Resolve("Use doc\nExec nope arg", config));
    }

    [Fact]
    public void Resolve_PresetWithUnknownSetting_Throws()
    {
        var config = Config("[preset.doc]\nbogusSetting = 5");
        var ex = Should.Throw<VcrConfigException>(() => Resolve("Use doc\nExec \"x\"", config));
        ex.Message.ShouldContain("bogusSetting");
    }

    [Fact]
    public void Resolve_InheritanceCycle_Throws()
    {
        var config = Config("""
            [preset.a]
            inherits = "b"
            [preset.b]
            inherits = "a"
            """);
        Should.Throw<VcrConfigException>(() => Resolve("Use a\nExec \"x\"", config));
    }

    [Fact]
    public void Resolve_UseWithoutConfig_Throws()
    {
        Should.Throw<VcrConfigException>(() => Resolve("Use doc\nExec \"x\"", config: null));
    }
}
