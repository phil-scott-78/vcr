using Shouldly;
using VcrSharp.Core.Rendering;
using VcrSharp.Core.Session;
using VcrSharp.Infrastructure.Rendering;

namespace VcrSharp.Core.Tests.Rendering;

/// <summary>
/// Tests for SvgRenderer output: CSS-variable color mode (Theme E),
/// intrinsic size + metadata (Theme D), and fit-to-content cropping (Theme C).
/// </summary>
public class SvgRendererTests
{
    private static TerminalCell Cell(string ch, string? fg = null, string? bg = null, int width = 1) =>
        new() { Character = ch, ForegroundColor = fg, BackgroundColor = bg, Width = width };

    private static TerminalContent Content(int cols, int rows, params TerminalCell[][] gridRows)
    {
        var cells = new TerminalCell[rows][];
        for (var r = 0; r < rows; r++)
        {
            if (r < gridRows.Length && gridRows[r].Length == cols)
            {
                cells[r] = gridRows[r];
            }
            else
            {
                var row = new TerminalCell[cols];
                for (var c = 0; c < cols; c++)
                    row[c] = r < gridRows.Length && c < gridRows[r].Length ? gridRows[r][c] : Cell(" ");
                cells[r] = row;
            }
        }
        return new TerminalContent { Cols = cols, Rows = rows, Cells = cells };
    }

    private static async Task<string> RenderStaticAsync(SessionOptions options, TerminalContent content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"vcr-svg-{Guid.NewGuid():N}.svg");
        try
        {
            var renderer = new SvgRenderer(options);
            if (options.FitToContent)
            {
                // Mirror the production wiring in FrameCapture/SvgEncoder.
                var extent = ContentExtent.Measure(content);
                renderer.SetContentExtent(extent.Cols, extent.Rows);
            }
            await renderer.RenderStaticAsync(path, content);
            return await File.ReadAllTextAsync(path);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static SessionOptions DeterministicOptions() => new()
    {
        Width = 200,
        Height = 100,
        FontSize = 20,
        ActualCellWidth = 10,
        ActualCellHeight = 20,
    };

    private static TerminalContent TextContent(int cols, int rows, params string[] lines)
    {
        var cells = new TerminalCell[rows][];
        for (var r = 0; r < rows; r++)
        {
            var row = new TerminalCell[cols];
            var line = r < lines.Length ? lines[r] : string.Empty;
            for (var c = 0; c < cols; c++)
                row[c] = Cell(c < line.Length ? line[c].ToString() : " ");
            cells[r] = row;
        }
        return new TerminalContent { Cols = cols, Rows = rows, Cells = cells };
    }

    private static async Task<string> RenderAnimatedAsync(
        SessionOptions options, IReadOnlyList<TerminalStateWithTime> states, double duration)
    {
        var path = Path.Combine(Path.GetTempPath(), $"vcr-svg-{Guid.NewGuid():N}.svg");
        try
        {
            await new SvgRenderer(options).RenderAnimatedAsync(path, states, duration);
            return await File.ReadAllTextAsync(path);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // Two states whose single row changes content, forcing a visibility <animate> to be emitted.
    private static IReadOnlyList<TerminalStateWithTime> TwoFrameStates() =>
    [
        new() { Content = TextContent(10, 1, "hi"), TimestampSeconds = 0.0 },
        new() { Content = TextContent(10, 1, "by"), TimestampSeconds = 1.0 },
    ];

    // ANSI green index "2", a truecolor cell, and an ANSI-red background "1".
    private static TerminalContent ColorSample() => Content(3, 1,
    [
        Cell("g", fg: "2"),
        Cell("x", fg: "#ff8801"),
        Cell(" ", bg: "1"),
    ]);

    [Fact]
    public async Task CssVariables_Off_EmitsLiteralHex_NoRootNoVars()
    {
        var options = DeterministicOptions();
        options.CssVariables = false;

        var svg = await RenderStaticAsync(options, ColorSample());

        svg.ShouldNotContain(":root{");
        svg.ShouldNotContain("var(--vcr");
        svg.ShouldContain(".g{fill:#0dbc79}");   // ANSI green literal
        svg.ShouldContain("#cd3131");            // ANSI red background literal
        svg.ShouldContain("#ff8801");            // truecolor stays literal
    }

    [Fact]
    public async Task CssVariables_On_EmitsRootBlockAndVarFills()
    {
        var options = DeterministicOptions();
        options.CssVariables = true;

        var svg = await RenderStaticAsync(options, ColorSample());

        svg.ShouldContain(":root{");
        svg.ShouldContain("--vcr-g:#0dbc79;");
        svg.ShouldContain("--vcr-bg:#1e1e1e;");
        svg.ShouldContain(".g{fill:var(--vcr-g,#0dbc79)}");
        svg.ShouldContain("var(--vcr-bg,#1e1e1e)");   // full-screen + shade background
        svg.ShouldContain("var(--vcr-r,#cd3131)");    // ANSI-red cell background
        svg.ShouldContain("#ff8801");                  // truecolor NOT var-ified
    }

    // ---- Theme D: intrinsic size + metadata ----

    /// <summary>Returns just the opening &lt;svg ...&gt; tag so attribute assertions ignore child elements.</summary>
    private static string RootTag(string svg)
    {
        var start = svg.IndexOf("<svg", StringComparison.Ordinal);
        var end = svg.IndexOf('>', start);
        return svg.Substring(start, end - start + 1);
    }

    [Fact]
    public async Task Metadata_DefaultOn_EmitsIntrinsicSizeAndDataAttributes()
    {
        var options = DeterministicOptions(); // SvgMetadata + SvgIntrinsicSize default true
        var content = Content(80, 24, new[] { Cell("h"), Cell("i") });

        var root = RootTag(await RenderStaticAsync(options, content));

        root.ShouldContain("width=\"200\"");
        root.ShouldContain("height=\"100\"");
        root.ShouldContain("viewBox=\"0 0 200 100\"");
        root.ShouldContain("data-cols=\"80\"");        // sourced from TerminalContent, not estimated
        root.ShouldContain("data-rows=\"24\"");
        root.ShouldContain("data-font-size=\"20\"");
        root.ShouldContain("data-cell-width=\"10\"");
        root.ShouldContain("data-cell-height=\"20\"");
    }

    [Fact]
    public async Task Metadata_Off_OmitsIntrinsicSizeAndDataAttributes()
    {
        var options = DeterministicOptions();
        options.SvgMetadata = false;
        options.SvgIntrinsicSize = false;
        var content = Content(80, 24, new[] { Cell("h"), Cell("i") });

        var root = RootTag(await RenderStaticAsync(options, content));

        root.ShouldContain("viewBox=\"0 0 200 100\""); // viewBox always present
        root.ShouldNotContain("width=");
        root.ShouldNotContain("height=");
        root.ShouldNotContain("data-cols");
        root.ShouldNotContain("data-font-size");
    }

    // ---- Theme C: fit-to-content cropping ----

    [Fact]
    public async Task FitToContent_CropsCanvasToContentExtentAndRelaxesClip()
    {
        var options = DeterministicOptions();
        options.FitToContent = true;
        var content = Content(80, 24, new[] { Cell("h"), Cell("i") }); // content only on row 0, cols 0-1

        var svg = await RenderStaticAsync(options, content);
        var root = RootTag(svg);

        // cell 10x20, padding 0 => 2 cols x 1 row => 20 x 20 canvas
        root.ShouldContain("viewBox=\"0 0 20 20\"");
        root.ShouldContain("width=\"20\"");
        root.ShouldContain("height=\"20\"");
        root.ShouldContain("data-cols=\"2\"");   // cropped extent, not the 80-col grid
        root.ShouldContain("data-rows=\"1\"");
        svg.ShouldNotContain("clip-path");        // relaxed in fit mode so the last row is not shaved
    }

    [Fact]
    public async Task AllSvgFeatures_Combined_ProduceWellFormedXml()
    {
        var options = DeterministicOptions();
        options.CssVariables = true;   // Theme E
        options.FitToContent = true;   // Theme C (metadata + intrinsic size default on = Theme D)
        var content = Content(80, 24, new[] { Cell("g", fg: "2"), Cell("o", fg: "2") });

        var svg = await RenderStaticAsync(options, content);

        Should.NotThrow(() => System.Xml.Linq.XDocument.Parse(svg)); // well-formed
        var root = RootTag(svg);
        root.ShouldContain("viewBox=\"0 0 20 20\"");          // C: cropped canvas
        root.ShouldContain("data-cols=\"2\"");                // D: metadata reports cropped extent
        svg.ShouldContain(":root{");                           // E: CSS variables
        svg.ShouldContain(".g{fill:var(--vcr-g,#0dbc79)}");
        svg.ShouldNotContain("clip-path");                     // C: clip relaxed in fit mode
    }

    [Fact]
    public async Task FitToContent_Off_KeepsFullCanvasAndClipPath()
    {
        var options = DeterministicOptions();
        options.FitToContent = false;
        var content = Content(80, 24, new[] { Cell("h"), Cell("i") });

        var svg = await RenderStaticAsync(options, content);

        RootTag(svg).ShouldContain("viewBox=\"0 0 200 100\"");
        svg.ShouldContain("clip-path=\"url(#terminal-clip)\"");
    }

    // ---- Theme A: loop control ----

    [Fact]
    public async Task Loop_Default_IsIndefiniteWithoutFreeze()
    {
        var options = DeterministicOptions();
        options.DisableCursor = true; // isolate row-visibility animates

        var svg = await RenderAnimatedAsync(options, TwoFrameStates(), 1.0);

        svg.ShouldContain("repeatCount=\"indefinite\"");
        svg.ShouldNotContain("fill=\"freeze\"");
    }

    [Fact]
    public async Task Loop_False_PlaysOnceAndFreezes()
    {
        var options = DeterministicOptions();
        options.DisableCursor = true;
        options.Loop = false;

        var svg = await RenderAnimatedAsync(options, TwoFrameStates(), 1.0);

        svg.ShouldContain("repeatCount=\"1\"");
        svg.ShouldContain("fill=\"freeze\"");
        svg.ShouldNotContain("repeatCount=\"indefinite\""); // cursor disabled, so nothing stays infinite
    }

    [Fact]
    public async Task LoopCount_EmitsCountAndFreezes()
    {
        var options = DeterministicOptions();
        options.DisableCursor = true;
        options.LoopCount = 3;

        var svg = await RenderAnimatedAsync(options, TwoFrameStates(), 1.0);

        svg.ShouldContain("repeatCount=\"3\"");
        svg.ShouldContain("fill=\"freeze\"");
    }

    [Fact]
    public async Task CursorOpacityBlink_StaysIndefinite_EvenWhenLoopFalse()
    {
        var options = DeterministicOptions();
        options.Loop = false;
        options.CursorBlink = true;
        options.DisableCursor = false;

        var svg = await RenderAnimatedAsync(options, TwoFrameStates(), 1.0);

        // Row animates freeze (play once) but the cursor blink keeps pulsing forever.
        svg.ShouldContain("fill=\"freeze\"");
        svg.ShouldContain("repeatCount=\"indefinite\"");
    }
}
