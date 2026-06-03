using Shouldly;
using VcrSharp.Core.Session;

namespace VcrSharp.Core.Tests.Session;

/// <summary>
/// Tests for the Loop/LoopCount resolvers that drive SVG repeatCount and GIF -loop.
/// </summary>
public class SessionOptionsLoopTests
{
    [Theory]
    [InlineData(true, null, "indefinite")]  // default: loop forever
    [InlineData(false, null, "1")]          // play once and hold
    [InlineData(true, 1, "1")]
    [InlineData(true, 3, "3")]
    [InlineData(false, 5, "5")]             // explicit count overrides Loop
    public void ResolveSvgRepeatCount(bool loop, int? count, string expected)
    {
        new SessionOptions { Loop = loop, LoopCount = count }
            .ResolveSvgRepeatCount().ShouldBe(expected);
    }

    [Theory]
    [InlineData(true, null, 0)]    // loop forever => ffmpeg -loop 0
    [InlineData(false, null, -1)]  // play once   => ffmpeg -loop -1
    [InlineData(true, 1, -1)]      // play once
    [InlineData(true, 2, 1)]       // play twice  => 1 repeat
    [InlineData(true, 3, 2)]       // play 3x     => 2 repeats
    public void ResolveGifLoopArgument(bool loop, int? count, int expected)
    {
        new SessionOptions { Loop = loop, LoopCount = count }
            .ResolveGifLoopArgument().ShouldBe(expected);
    }

    [Fact]
    public void Defaults_AreInfiniteLoop()
    {
        var options = new SessionOptions();
        options.Loop.ShouldBeTrue();
        options.LoopCount.ShouldBeNull();
        options.ResolveSvgRepeatCount().ShouldBe("indefinite");
        options.ResolveGifLoopArgument().ShouldBe(0);
    }
}
