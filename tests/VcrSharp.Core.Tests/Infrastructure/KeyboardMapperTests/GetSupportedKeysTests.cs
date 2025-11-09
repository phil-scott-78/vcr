using Shouldly;
using VcrSharp.Infrastructure.Playwright;

namespace VcrSharp.Core.Tests.Infrastructure.KeyboardMapperTests;

/// <summary>
/// Tests for KeyboardMapper.GetSupportedKeys method.
/// </summary>
public class GetSupportedKeysTests
{
    [Fact]
    public void GetSupportedKeys_ReturnsNonEmptyList()
    {
        var keys = KeyboardMapper.GetSupportedKeys();

        keys.ShouldNotBeEmpty();
        keys.ShouldContain("Enter");
        keys.ShouldContain("Backspace");
        keys.ShouldContain("Up");
        keys.ShouldContain("F1");
    }
}