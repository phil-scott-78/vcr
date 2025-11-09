using Shouldly;
using VcrSharp.Infrastructure.Playwright;

namespace VcrSharp.Core.Tests.Infrastructure.KeyboardMapperTests;

/// <summary>
/// Tests for KeyboardMapper.MapKey method.
/// </summary>
public class MapKeyTests
{
    [Theory]
    [InlineData("Enter", "Enter")]
    [InlineData("Return", "Enter")]
    [InlineData("Tab", "Tab")]
    [InlineData("Escape", "Escape")]
    [InlineData("Esc", "Escape")]
    [InlineData("Space", "Space")]
    public void MapKey_NavigationKeys_ReturnsCorrectPlaywrightKey(string input, string expected)
    {
        var result = KeyboardMapper.MapKey(input);
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Backspace", "Backspace")]
    [InlineData("Delete", "Delete")]
    [InlineData("Del", "Delete")]
    public void MapKey_EditingKeys_ReturnsCorrectPlaywrightKey(string input, string expected)
    {
        var result = KeyboardMapper.MapKey(input);
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Up", "ArrowUp")]
    [InlineData("Down", "ArrowDown")]
    [InlineData("Left", "ArrowLeft")]
    [InlineData("Right", "ArrowRight")]
    public void MapKey_ArrowKeys_ReturnsCorrectPlaywrightKey(string input, string expected)
    {
        var result = KeyboardMapper.MapKey(input);
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Home", "Home")]
    [InlineData("End", "End")]
    [InlineData("PageUp", "PageUp")]
    [InlineData("PageDown", "PageDown")]
    public void MapKey_HomeEndPageKeys_ReturnsCorrectPlaywrightKey(string input, string expected)
    {
        var result = KeyboardMapper.MapKey(input);
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("F1", "F1")]
    [InlineData("F5", "F5")]
    [InlineData("F12", "F12")]
    public void MapKey_FunctionKeys_ReturnsCorrectPlaywrightKey(string input, string expected)
    {
        var result = KeyboardMapper.MapKey(input);
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Insert", "Insert")]
    [InlineData("Ins", "Insert")]
    public void MapKey_InsertKey_ReturnsCorrectPlaywrightKey(string input, string expected)
    {
        var result = KeyboardMapper.MapKey(input);
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("a", "a")]
    [InlineData("Z", "Z")]
    [InlineData("1", "1")]
    [InlineData("!", "!")]
    public void MapKey_SingleCharacter_ReturnsSameCharacter(string input, string expected)
    {
        var result = KeyboardMapper.MapKey(input);
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MapKey_NullOrWhitespace_ReturnsNull(string? input)
    {
        var result = KeyboardMapper.MapKey(input!);
        result.ShouldBeNull();
    }

    [Theory]
    [InlineData("UnknownKey")]
    [InlineData("NotAKey")]
    public void MapKey_UnrecognizedKey_ReturnsNull(string input)
    {
        var result = KeyboardMapper.MapKey(input);
        result.ShouldBeNull();
    }
}