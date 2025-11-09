using Shouldly;
using VcrSharp.Infrastructure.Playwright;

namespace VcrSharp.Core.Tests.Infrastructure.KeyboardMapperTests;

/// <summary>
/// Tests for KeyboardMapper.MapModifier method.
/// </summary>
public class MapModifierTests
{
    [Theory]
    [InlineData("Ctrl", "Control")]
    [InlineData("Control", "Control")]
    public void MapModifier_Ctrl_ReturnsControl(string input, string expected)
    {
        var result = KeyboardMapper.MapModifier(input);
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Alt", "Alt")]
    public void MapModifier_Alt_ReturnsAlt(string input, string expected)
    {
        var result = KeyboardMapper.MapModifier(input);
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Shift", "Shift")]
    public void MapModifier_Shift_ReturnsShift(string input, string expected)
    {
        var result = KeyboardMapper.MapModifier(input);
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Meta", "Meta")]
    [InlineData("Cmd", "Meta")]
    [InlineData("Command", "Meta")]
    [InlineData("Super", "Meta")]
    [InlineData("Win", "Meta")]
    public void MapModifier_MetaVariants_ReturnsMeta(string input, string expected)
    {
        var result = KeyboardMapper.MapModifier(input);
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MapModifier_NullOrWhitespace_ReturnsNull(string? input)
    {
        var result = KeyboardMapper.MapModifier(input!);
        result.ShouldBeNull();
    }

    [Theory]
    [InlineData("NotAModifier")]
    [InlineData("Enter")]
    public void MapModifier_UnrecognizedModifier_ReturnsNull(string input)
    {
        var result = KeyboardMapper.MapModifier(input);
        result.ShouldBeNull();
    }
}