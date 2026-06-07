using System.Text;
using Shouldly;

namespace VcrSharp.Terminal.Tests.Conformance;

/// <summary>
/// Robustness gate: the parser must never throw, hang, or corrupt its own state on adversarial input —
/// pure random bytes, dense escape soup, and truncated/abused sequences. (The full Williams VT500
/// transition oracle arrives with the P1 table-driven parser rewrite, which makes states observable;
/// until then this no-crash fuzzing is the parser-robustness gate.)
/// </summary>
public sealed class FuzzTests
{
    private static readonly string E = ((char)0x1b).ToString(); // ESC, built without a unicode escape literal

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(12345)]
    public void RandomBytes_NeverThrow(int seed)
    {
        var rng = new Random(seed);
        var screen = new VtScreen(80, 25);
        var chunk = new byte[4096];
        for (var iter = 0; iter < 50; iter++)
        {
            rng.NextBytes(chunk);
            Should.NotThrow(() => screen.Feed(Encoding.Latin1.GetString(chunk)));
        }
        Should.NotThrow(() => screen.ToTerminalContent());
    }

    [Theory]
    [InlineData(7)]
    [InlineData(99)]
    public void EscapeSoup_NeverThrow(int seed)
    {
        var rng = new Random(seed);
        // A vocabulary that exercises every parser entry point, including pathological/truncated forms.
        string[] frags =
        {
            "", E, E + "[", E + "]", E + "P", E + "X", E + "^", E + "_", E + "\\", E + "(", E + ")", E + "#", E + "%",
            E + "[31m", E + "[1;2;3m", E + "[38;2;255;0;0m", E + "[38:2::255:0:0m", E + "[?25l", E + "[?1049h",
            E + "[2J", E + "[H", E + "[10;20H", E + "[5A", E + "[3P", E + "[2@", E + "[K", E + "[?", E + "[;;;;;m",
            E + "]0;title\a", E + "]8;;https://example.com" + E + "\\link" + E + "]8;;" + E + "\\",
            E + "Pq#0;2;0;0;0" + E + "\\",
            "[[[[", "\b\b\b", "\r\n", "\t", "world", "世界", "🎸",
            E + "[999999999999999999m", E + "[" + new string('1', 200) + "m",
        };
        var screen = new VtScreen(80, 25);
        var sb = new StringBuilder();
        for (var i = 0; i < 5000; i++) sb.Append(frags[rng.Next(frags.Length)]);
        Should.NotThrow(() => screen.Feed(sb.ToString()));
        Should.NotThrow(() => screen.ToTerminalContent());
    }

    [Fact]
    public void HugeParameterCount_DoesNotBlowUp()
    {
        // No param cap today (a P1 fix) — but it must at least not OOM/throw on a long SGR list.
        var screen = new VtScreen(80, 25);
        var huge = E + "[" + string.Join(";", Enumerable.Repeat("1", 5000)) + "mX";
        Should.NotThrow(() => screen.Feed(huge));
    }
}
