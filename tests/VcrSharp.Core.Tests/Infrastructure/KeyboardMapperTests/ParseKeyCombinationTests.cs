using Shouldly;
using VcrSharp.Infrastructure.Playwright;

namespace VcrSharp.Core.Tests.Infrastructure.KeyboardMapperTests;

/// <summary>
/// Tests for KeyboardMapper.ParseKeyCombination method.
/// </summary>
public class ParseKeyCombinationTests
{
    [Fact]
    public void ParseKeyCombination_CtrlC_ReturnsCorrectParsing()
    {
        var result = KeyboardMapper.ParseKeyCombination("Ctrl+C");

        result.ShouldNotBeNull();
        result.Value.Modifiers.ShouldBe(["Control"]);
        result.Value.Key.ShouldBe("C");
    }

    [Fact]
    public void ParseKeyCombination_AltEnter_ReturnsCorrectParsing()
    {
        var result = KeyboardMapper.ParseKeyCombination("Alt+Enter");

        result.ShouldNotBeNull();
        result.Value.Modifiers.ShouldBe(["Alt"]);
        result.Value.Key.ShouldBe("Enter");
    }

    [Fact]
    public void ParseKeyCombination_CtrlShiftTab_ReturnsCorrectParsing()
    {
        var result = KeyboardMapper.ParseKeyCombination("Ctrl+Shift+Tab");

        result.ShouldNotBeNull();
        result.Value.Modifiers.ShouldBe(["Control", "Shift"]);
        result.Value.Key.ShouldBe("Tab");
    }

    [Fact]
    public void ParseKeyCombination_CtrlAltDelete_ReturnsCorrectParsing()
    {
        var result = KeyboardMapper.ParseKeyCombination("Ctrl+Alt+Delete");

        result.ShouldNotBeNull();
        result.Value.Modifiers.ShouldBe(["Control", "Alt"]);
        result.Value.Key.ShouldBe("Delete");
    }

    [Fact]
    public void ParseKeyCombination_SingleKey_ReturnsNoModifiers()
    {
        var result = KeyboardMapper.ParseKeyCombination("Enter");

        result.ShouldNotBeNull();
        result.Value.Modifiers.ShouldBeEmpty();
        result.Value.Key.ShouldBe("Enter");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseKeyCombination_NullOrWhitespace_ReturnsNull(string? input)
    {
        var result = KeyboardMapper.ParseKeyCombination(input!);
        result.ShouldBeNull();
    }

    [Fact]
    public void ParseKeyCombination_InvalidModifier_ReturnsNull()
    {
        var result = KeyboardMapper.ParseKeyCombination("NotAModifier+C");
        result.ShouldBeNull();
    }

    [Fact]
    public void ParseKeyCombination_InvalidKey_ReturnsNull()
    {
        var result = KeyboardMapper.ParseKeyCombination("Ctrl+NotAKey");
        result.ShouldBeNull();
    }
}