using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using VcrSharp.Core.Rendering;
using VcrSharp.Core.Session;

namespace VcrSharp.Infrastructure.Rendering;

/// <summary>
/// Renders a <see cref="TerminalContent"/> cell grid to a raster image with ImageSharp (no browser),
/// so the native path can produce GIF/MP4/WebM/PNG by rasterizing each captured frame and feeding the
/// frames to FFmpeg. Uses the same cell metrics and theme/palette resolution as the SVG path; glyphs
/// (incl. box-drawing/braille/CJK) come from a real monospace font, so the font must cover them.
/// </summary>
public sealed class RasterRenderer
{
    private readonly SessionOptions _options;
    private readonly Font _regular, _bold, _italic, _boldItalic;
    private readonly float _cellW, _cellH;
    private readonly Color _bg, _fg, _cursor;

    // Background rects/decorations are axis-aligned on integer cell boundaries; anti-aliasing their
    // edges makes adjacent same-color cells (bar fills) show hairline seams, so fill them crisp.
    private static readonly DrawingOptions NoAntialias = new() { GraphicsOptions = new GraphicsOptions { Antialias = false } };

    public RasterRenderer(SessionOptions options)
    {
        _options = options;
        var family = ResolveFontFamily(options.FontFamily);
        float size = options.FontSize;
        _regular = family.CreateFont(size, FontStyle.Regular);
        _bold = family.CreateFont(size, FontStyle.Bold);
        _italic = family.CreateFont(size, FontStyle.Italic);
        _boldItalic = family.CreateFont(size, FontStyle.BoldItalic);
        _cellW = (float)Math.Round(options.ActualCellWidth ?? options.FontSize * 0.6);
        _cellH = (float)Math.Round(options.ActualCellHeight ?? options.FontSize * 1.2);
        _bg = Hex(options.Theme.Background, Color.Black);
        _fg = Hex(options.Theme.Foreground, Color.White);
        _cursor = Hex(options.Theme.Cursor, _fg);
    }

    public int Width(TerminalContent c) => (int)Math.Round(c.Cols * _cellW) + 2 * _options.Padding;
    public int Height(TerminalContent c) => (int)Math.Round(c.Rows * _cellH) + 2 * _options.Padding;

    public Image<Rgba32> Render(TerminalContent content)
    {
        var pad = _options.Padding;
        int width = Width(content), height = Height(content);

        // Guard against an absurd pixel budget (huge Cols/Rows or FontSize) before ImageSharp tries to
        // allocate it — fail with a clear message instead of an OutOfMemoryException.
        const long MaxPixels = 100_000_000; // ~10k×10k
        if (width < 1 || height < 1 || (long)width * height > MaxPixels)
            throw new InvalidOperationException(
                $"Raster output {width}×{height}px is outside the supported range; reduce Cols/Rows or FontSize.");

        var image = new Image<Rgba32>(width, height);

        image.Mutate(ctx =>
        {
            if (!_options.TransparentBackground) ctx.BackgroundColor(_bg);

            for (var r = 0; r < content.Rows; r++)
            {
                for (var col = 0; col < content.Cols; col++)
                {
                    var cell = content.Cells[r][col];
                    if (cell.Width == 0) continue; // continuation of a wide glyph

                    var x = pad + col * _cellW;
                    var y = pad + r * _cellH;
                    var w = (cell.Width == 2 ? 2 : 1) * _cellW;

                    var fg = cell.ForegroundColor is { } f ? Hex(Resolve(f), _fg) : _fg;
                    var hasBg = cell.BackgroundColor is not null;
                    var bg = hasBg ? Hex(Resolve(cell.BackgroundColor!), _bg) : _bg;
                    if (cell.IsReverse) (fg, bg, hasBg) = (bg, fg, true);
                    if (cell.IsDim) fg = fg.WithAlpha(0.55f);

                    if (hasBg) ctx.Fill(NoAntialias, bg, new RectangleF(x, y, w, _cellH));

                    var ch = cell.Character;
                    if (!cell.IsConceal && ch.Length > 0 && ch != " ")
                    {
                        if (ch.Length == 1 && ch[0] == '█')
                        {
                            // Full block (█) — fill solid instead of drawing the glyph, so contiguous
                            // blocks (bar/breakdown charts) tile seamlessly without glyph-AA seams.
                            ctx.Fill(NoAntialias, fg, new RectangleF(x, y, w, _cellH));
                        }
                        else
                        {
                            var font = cell.IsBold
                                ? (cell.IsItalic ? _boldItalic : _bold)
                                : (cell.IsItalic ? _italic : _regular);
                            var text = new RichTextOptions(font)
                            {
                                Origin = new PointF(x, y),
                                VerticalAlignment = VerticalAlignment.Top,
                                HorizontalAlignment = HorizontalAlignment.Left,
                            };
                            ctx.DrawText(text, ch, fg);
                        }
                    }

                    if (cell.IsUnderline) ctx.Fill(NoAntialias, fg, new RectangleF(x, y + _cellH - 2, w, 1.5f));
                    if (cell.IsStrikethrough) ctx.Fill(NoAntialias, fg, new RectangleF(x, y + _cellH * 0.5f, w, 1.5f));
                    if (cell.IsOverline) ctx.Fill(NoAntialias, fg, new RectangleF(x, y, w, 1.5f));
                }
            }

            if (content.CursorVisible && !_options.DisableCursor &&
                content.CursorY < content.Rows && content.CursorX < content.Cols)
            {
                var cx = pad + content.CursorX * _cellW;
                var cy = pad + content.CursorY * _cellH;
                ctx.Fill(NoAntialias, _cursor.WithAlpha(0.6f), new RectangleF(cx, cy, _cellW, _cellH));
            }
        });

        return image;
    }

    // ---- palette / color resolution (mirrors SvgRenderer) ----

    private string Resolve(string color)
    {
        if (color.StartsWith('#')) return color;
        if (!int.TryParse(color, out var idx)) return color;
        return idx switch
        {
            0 => _options.Theme.Black, 1 => _options.Theme.Red, 2 => _options.Theme.Green, 3 => _options.Theme.Yellow,
            4 => _options.Theme.Blue, 5 => _options.Theme.Magenta, 6 => _options.Theme.Cyan, 7 => _options.Theme.White,
            8 => _options.Theme.BrightBlack, 9 => _options.Theme.BrightRed, 10 => _options.Theme.BrightGreen,
            11 => _options.Theme.BrightYellow, 12 => _options.Theme.BrightBlue, 13 => _options.Theme.BrightMagenta,
            14 => _options.Theme.BrightCyan, 15 => _options.Theme.BrightWhite,
            _ => PaletteIndexToHex(idx),
        };
    }

    /// <summary>xterm 256-color palette: 16-231 = 6×6×6 cube, 232-255 = grayscale ramp.</summary>
    private static string PaletteIndexToHex(int idx)
    {
        if (idx is < 16 or > 255) return "#000000";
        if (idx >= 232)
        {
            var g = 8 + (idx - 232) * 10;
            return $"#{g:x2}{g:x2}{g:x2}";
        }
        var n = idx - 16;
        int Comp(int v) => v == 0 ? 0 : 55 + v * 40;
        var r = Comp(n / 36);
        var gg = Comp(n / 6 % 6);
        var b = Comp(n % 6);
        return $"#{r:x2}{gg:x2}{b:x2}";
    }

    private static Color Hex(string hex, Color fallback)
    {
        try { return Color.ParseHex(hex.TrimStart('#')); }
        catch { return fallback; }
    }

    private static FontFamily ResolveFontFamily(string requested)
    {
        // The configured FontFamily is often a generic name ("monospace") that no installed font matches;
        // fall back to a real monospace the system has.
        foreach (var name in new[] { requested, "Cascadia Mono", "Cascadia Code", "Consolas", "Courier New", "DejaVu Sans Mono" })
            if (!string.IsNullOrWhiteSpace(name) && SystemFonts.TryGet(name, out var family))
                return family;
        // First() throws an opaque InvalidOperationException on a system with no fonts — give an actionable one.
        var families = SystemFonts.Families;
        var fontFamilies = families as FontFamily[] ?? families.ToArray();
        if (fontFamilies.Length != 0) return fontFamilies.First();
        throw new InvalidOperationException(
            "No fonts are installed; raster output (GIF/MP4/WebM/PNG) needs a monospace font (e.g. Cascadia Mono). Use SVG output, or install a font.");
    }
}
