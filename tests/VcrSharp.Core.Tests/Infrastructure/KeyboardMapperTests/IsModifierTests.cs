using Shouldly;
using VcrSharp.Infrastructure.Playwright;

namespace VcrSharp.Core.Tests.Infrastructure.KeyboardMapperTests;

/// <summary>
/// Tests for KeyboardMapper.IsModifier method.
/// </summary>
public class IsModifierTests
{
    [Theory]
    [InlineData("Ctrl", true)]
    [InlineData("Alt", true)]
    [InlineData("Shift", true)]
    [InlineData("Meta", true)]
    [InlineData("Enter", false)]
    [InlineData("a", false)]
    [InlineData("NotAKey", false)]
    public void IsModifier_VariousInputs_ReturnsExpectedResult(string input, bool expected)
    {
        var result = KeyboardMapper.IsModifier(input);
        result.ShouldBe(expected);
    }
}