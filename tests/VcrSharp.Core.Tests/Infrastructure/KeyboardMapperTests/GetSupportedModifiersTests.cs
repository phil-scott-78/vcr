using Shouldly;
using VcrSharp.Infrastructure.Playwright;

namespace VcrSharp.Core.Tests.Infrastructure.KeyboardMapperTests;

/// <summary>
/// Tests for KeyboardMapper.GetSupportedModifiers method.
/// </summary>
public class GetSupportedModifiersTests
{
    [Fact]
    public void GetSupportedModifiers_ReturnsNonEmptyList()
    {
        var modifiers = KeyboardMapper.GetSupportedModifiers();

        modifiers.ShouldNotBeEmpty();
        modifiers.ShouldContain("Ctrl");
        modifiers.ShouldContain("Alt");
        modifiers.ShouldContain("Shift");
    }
}