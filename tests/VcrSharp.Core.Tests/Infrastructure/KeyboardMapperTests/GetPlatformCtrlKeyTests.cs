using Shouldly;
using VcrSharp.Infrastructure.Playwright;

namespace VcrSharp.Core.Tests.Infrastructure.KeyboardMapperTests;

/// <summary>
/// Tests for KeyboardMapper.GetPlatformCtrlKey method.
/// </summary>
public class GetPlatformCtrlKeyTests
{
    [Fact]
    public void GetPlatformCtrlKey_ReturnsValidModifier()
    {
        var result = KeyboardMapper.GetPlatformCtrlKey();

        // Should be either "Control" or "Meta" depending on platform
        result.ShouldBeOneOf("Control", "Meta");
    }
}