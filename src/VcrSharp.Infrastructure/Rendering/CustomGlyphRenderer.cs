using System.Globalization;
using System.Text;

namespace VcrSharp.Infrastructure.Rendering;

/// <summary>
/// Renders box-drawing, block elements, and powerline characters as custom SVG paths
/// instead of relying on font glyphs. This ensures pixel-perfect alignment and consistent
/// rendering across all platforms.
/// </summary>
public static class CustomGlyphRenderer
{
    /// <summary>
    /// Checks if a character should be rendered as a custom glyph.
    /// </summary>
    public static bool IsCustomGlyph(string character)
    {
        if (string.IsNullOrEmpty(character) || character.Length != 1)
            return false;

        var c = character[0];
        return c switch
        {
            // Box Drawing: U+2500–U+257F
            >= '\u2500' and <= '\u257F' => true,
            // Block Elements: U+2580–U+259F
            >= '\u2580' and <= '\u259F' => true,
            // Powerline symbols: U+E0A0–U+E0A3, U+E0B0–U+E0D4
            >= '\uE0A0' and <= '\uE0A3' => true,
            >= '\uE0B0' and <= '\uE0D4' => true,
            _ => false
        };
    }

    /// <summary>
    /// Generates SVG elements for a custom glyph character.
    /// Returns the complete SVG element(s) as a string, or null if not a custom glyph.
    /// </summary>
    public static string? RenderGlyph(char c, double x, double y, double cellWidth, double cellHeight,
        string foregroundColor, string? backgroundColor, double lightStrokeWidth, double heavyStrokeWidth)
    {
        var sb = new StringBuilder();

        // Render background first if present
        if (backgroundColor != null)
        {
            sb.Append(
                $"<rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(cellWidth)}\" height=\"{F(cellHeight)}\" fill=\"{backgroundColor}\"/>");
        }

        switch (c)
        {
            // Determine glyph type and render
            case >= '\u2500' and <= '\u257F':
                RenderBoxDrawing(sb, c, x, y, cellWidth, cellHeight, foregroundColor, lightStrokeWidth,
                    heavyStrokeWidth);
                break;
            case >= '\u2580' and <= '\u259F':
                RenderBlockElement(sb, c, x, y, cellWidth, cellHeight, foregroundColor);
                break;
            case >= '\uE0A0' and <= '\uE0A3':
            case >= '\uE0B0' and <= '\uE0D4':
                RenderPowerline(sb, c, x, y, cellWidth, cellHeight, foregroundColor);
                break;
            default:
                return null;
        }

        return sb.ToString();
    }

    // Box drawing characters are encoded with bits indicating which directions have lines:
    // Bit layout for standard chars: [up-heavy][down-heavy][left-heavy][right-heavy][up][down][left][right]
    // We use a lookup table for the ~128 characters

    private static void RenderBoxDrawing(StringBuilder sb, char c, double x, double y,
        double w, double h, string color, double lightStroke, double heavyStroke)
    {
        var centerX = x + w / 2;
        var centerY = y + h / 2;
        var gap = lightStroke * 1.5; // gap between double lines

        switch (c)
        {
            // Handle special double-line characters that need custom geometry
            // ╔ ╗ ╚ ╝
            case '\u2554' or '\u2557' or '\u255A' or '\u255D':
                RenderDoubleCorner(sb, c, x, y, w, h, color, lightStroke, gap);
                return;
            // ╠ ╣ ╦ ╩
            case '\u2560' or '\u2563' or '\u2566' or '\u2569':
                RenderDoubleTJunction(sb, c, x, y, w, h, color, lightStroke, gap);
                return;
            // ╬
            case '\u256C':
                RenderDoubleCross(sb, c, x, y, w, h, color, lightStroke, gap);
                return;
        }

        // Get the line segments for this character
        var segments = GetBoxDrawingSegments(c);
        if (segments == BoxSegments.None) return;

        // Build path data
        var pathData = new StringBuilder();

        // Light horizontal line (left to center or center to right)
        if ((segments & BoxSegments.LightLeft) != 0)
            pathData.Append($"M{F(x)} {F(centerY)}H{F(centerX)}");
        if ((segments & BoxSegments.LightRight) != 0)
            pathData.Append($"M{F(centerX)} {F(centerY)}H{F(x + w)}");
        if ((segments & BoxSegments.LightUp) != 0)
            pathData.Append($"M{F(centerX)} {F(y)}V{F(centerY)}");
        if ((segments & BoxSegments.LightDown) != 0)
            pathData.Append($"M{F(centerX)} {F(centerY)}V{F(y + h)}");

        if (pathData.Length > 0)
        {
            sb.Append(
                $"<path d=\"{pathData}\" stroke=\"{color}\" stroke-width=\"{F(lightStroke)}\" stroke-linecap=\"square\" fill=\"none\"/>");
        }

        // Heavy lines (thicker stroke)
        var heavyPath = new StringBuilder();
        if ((segments & BoxSegments.HeavyLeft) != 0)
            heavyPath.Append($"M{F(x)} {F(centerY)}H{F(centerX)}");
        if ((segments & BoxSegments.HeavyRight) != 0)
            heavyPath.Append($"M{F(centerX)} {F(centerY)}H{F(x + w)}");
        if ((segments & BoxSegments.HeavyUp) != 0)
            heavyPath.Append($"M{F(centerX)} {F(y)}V{F(centerY)}");
        if ((segments & BoxSegments.HeavyDown) != 0)
            heavyPath.Append($"M{F(centerX)} {F(centerY)}V{F(y + h)}");

        if (heavyPath.Length > 0)
        {
            sb.Append(
                $"<path d=\"{heavyPath}\" stroke=\"{color}\" stroke-width=\"{F(heavyStroke)}\" stroke-linecap=\"square\" fill=\"none\"/>");
        }

        // Double lines (two parallel lines) - for straight segments only
        // (corners, T-junctions, and crosses are handled above)
        if ((segments & BoxSegments.DoubleHorizontal) != 0)
        {
            sb.Append(
                $"<path d=\"M{F(x)} {F(centerY - gap)}H{F(x + w)}M{F(x)} {F(centerY + gap)}H{F(x + w)}\" stroke=\"{color}\" stroke-width=\"{F(lightStroke)}\" fill=\"none\"/>");
        }

        if ((segments & BoxSegments.DoubleVertical) != 0)
        {
            sb.Append(
                $"<path d=\"M{F(centerX - gap)} {F(y)}V{F(y + h)}M{F(centerX + gap)} {F(y)}V{F(y + h)}\" stroke=\"{color}\" stroke-width=\"{F(lightStroke)}\" fill=\"none\"/>");
        }

        // Dashed lines
        if ((segments & BoxSegments.DashedHorizontal) != 0)
        {
            sb.Append(
                $"<path d=\"M{F(x)} {F(centerY)}H{F(x + w)}\" stroke=\"{color}\" stroke-width=\"{F(lightStroke)}\" stroke-dasharray=\"{F(w / 4)} {F(w / 8)}\" fill=\"none\"/>");
        }

        if ((segments & BoxSegments.DashedVertical) != 0)
        {
            sb.Append(
                $"<path d=\"M{F(centerX)} {F(y)}V{F(y + h)}\" stroke=\"{color}\" stroke-width=\"{F(lightStroke)}\" stroke-dasharray=\"{F(h / 4)} {F(h / 8)}\" fill=\"none\"/>");
        }

        // Rounded corners (arc instead of sharp corner)
        if ((segments & BoxSegments.RoundedCorner) != 0)
        {
            RenderRoundedCorner(sb, c, x, y, w, h, centerX, centerY, color, lightStroke);
        }
    }

    private static void RenderRoundedCorner(StringBuilder sb, char c, double x, double y,
        double w, double h, double cx, double cy, string color, double stroke)
    {
        var r = Math.Min(w, h) / 3; // radius for rounded corner
        var path = c switch
        {
            '\u256D' => $"M{F(cx)} {F(y + h)}V{F(cy + r)}A{F(r)} {F(r)} 0 0 1 {F(cx + r)} {F(cy)}H{F(x + w)}", // ╭
            '\u256E' => $"M{F(x)} {F(cy)}H{F(cx - r)}A{F(r)} {F(r)} 0 0 1 {F(cx)} {F(cy + r)}V{F(y + h)}", // ╮
            '\u256F' => $"M{F(cx)} {F(y)}V{F(cy - r)}A{F(r)} {F(r)} 0 0 1 {F(cx - r)} {F(cy)}H{F(x)}", // ╯
            '\u2570' => $"M{F(x + w)} {F(cy)}H{F(cx + r)}A{F(r)} {F(r)} 0 0 1 {F(cx)} {F(cy - r)}V{F(y)}", // ╰
            _ => ""
        };

        if (!string.IsNullOrEmpty(path))
        {
            sb.Append(
                $"<path d=\"{path}\" stroke=\"{color}\" stroke-width=\"{F(stroke)}\" stroke-linecap=\"round\" fill=\"none\"/>");
        }
    }

    /// <summary>
    /// Renders double-line corners (╔ ╗ ╚ ╝) with proper geometry.
    /// Each corner has 4 line segments that meet at specific points.
    /// </summary>
    private static void RenderDoubleCorner(StringBuilder sb, char c, double x, double y,
        double w, double h, string color, double stroke, double gap)
    {
        var left = x;
        var right = x + w;
        var top = y;
        var bottom = y + h;
        var cx = x + w / 2;
        var cy = y + h / 2;

        // Outer and inner line positions
        var outerV = cx - gap; // left vertical of the pair
        var innerV = cx + gap; // right vertical of the pair
        var outerH = cy - gap; // top horizontal of the pair
        var innerH = cy + gap; // bottom horizontal of the pair

        var pathData = c switch
        {
            '\u2554' => // ╔ - down and right
                $"M{F(outerV)} {F(outerH)}H{F(right)}" + // outer horizontal: corner to right
                $"M{F(innerV)} {F(innerH)}H{F(right)}" + // inner horizontal: corner to right
                $"M{F(outerV)} {F(outerH)}V{F(bottom)}" + // outer vertical: corner down
                $"M{F(innerV)} {F(innerH)}V{F(bottom)}", // inner vertical: corner down

            '\u2557' => // ╗ - down and left
                $"M{F(innerV)} {F(outerH)}H{F(left)}" + // outer horizontal: corner to left
                $"M{F(outerV)} {F(innerH)}H{F(left)}" + // inner horizontal: corner to left
                $"M{F(innerV)} {F(outerH)}V{F(bottom)}" + // outer vertical: corner down
                $"M{F(outerV)} {F(innerH)}V{F(bottom)}", // inner vertical: corner down

            '\u255A' => // ╚ - up and right
                $"M{F(outerV)} {F(innerH)}H{F(right)}" + // outer horizontal: corner to right
                $"M{F(innerV)} {F(outerH)}H{F(right)}" + // inner horizontal: corner to right
                $"M{F(outerV)} {F(innerH)}V{F(top)}" + // outer vertical: corner up
                $"M{F(innerV)} {F(outerH)}V{F(top)}", // inner vertical: corner up

            '\u255D' => // ╝ - up and left
                $"M{F(innerV)} {F(innerH)}H{F(left)}" + // outer horizontal: corner to left
                $"M{F(outerV)} {F(outerH)}H{F(left)}" + // inner horizontal: corner to left
                $"M{F(innerV)} {F(innerH)}V{F(top)}" + // outer vertical: corner up
                $"M{F(outerV)} {F(outerH)}V{F(top)}", // inner vertical: corner up

            _ => ""
        };

        if (!string.IsNullOrEmpty(pathData))
        {
            sb.Append(
                $"<path d=\"{pathData}\" stroke=\"{color}\" stroke-width=\"{F(stroke)}\" stroke-linecap=\"square\" fill=\"none\"/>");
        }
    }

    /// <summary>
    /// Renders double-line T-junctions (╠ ╣ ╦ ╩) with proper geometry.
    /// All lines are double (two parallel lines) including the branch.
    /// </summary>
    private static void RenderDoubleTJunction(StringBuilder sb, char c, double x, double y,
        double w, double h, string color, double stroke, double gap)
    {
        var left = x;
        var right = x + w;
        var top = y;
        var bottom = y + h;
        var cx = x + w / 2;
        var cy = y + h / 2;

        var outerV = cx - gap;
        var innerV = cx + gap;
        var outerH = cy - gap;
        var innerH = cy + gap;

        var pathData = c switch
        {
            '\u2560' => // ╠ - double vertical and double right
                $"M{F(outerV)} {F(top)}V{F(bottom)}" + // left vertical line (full)
                $"M{F(innerV)} {F(top)}V{F(outerH)}" + // right vertical line (top portion)
                $"M{F(innerV)} {F(innerH)}V{F(bottom)}" + // right vertical line (bottom portion)
                $"M{F(innerV)} {F(outerH)}H{F(right)}" + // top horizontal line to right
                $"M{F(innerV)} {F(innerH)}H{F(right)}", // bottom horizontal line to right

            '\u2563' => // ╣ - double vertical and double left
                $"M{F(innerV)} {F(top)}V{F(bottom)}" + // right vertical line (full)
                $"M{F(outerV)} {F(top)}V{F(outerH)}" + // left vertical line (top portion)
                $"M{F(outerV)} {F(innerH)}V{F(bottom)}" + // left vertical line (bottom portion)
                $"M{F(outerV)} {F(outerH)}H{F(left)}" + // top horizontal line to left
                $"M{F(outerV)} {F(innerH)}H{F(left)}", // bottom horizontal line to left

            '\u2566' => // ╦ - double horizontal and double down
                $"M{F(left)} {F(outerH)}H{F(right)}" + // top horizontal line (full)
                $"M{F(left)} {F(innerH)}H{F(outerV)}" + // bottom horizontal line (left portion)
                $"M{F(innerV)} {F(innerH)}H{F(right)}" + // bottom horizontal line (right portion)
                $"M{F(outerV)} {F(innerH)}V{F(bottom)}" + // left vertical line down
                $"M{F(innerV)} {F(innerH)}V{F(bottom)}", // right vertical line down

            '\u2569' => // ╩ - double horizontal and double up
                $"M{F(left)} {F(innerH)}H{F(right)}" + // bottom horizontal line (full)
                $"M{F(left)} {F(outerH)}H{F(outerV)}" + // top horizontal line (left portion)
                $"M{F(innerV)} {F(outerH)}H{F(right)}" + // top horizontal line (right portion)
                $"M{F(outerV)} {F(outerH)}V{F(top)}" + // left vertical line up
                $"M{F(innerV)} {F(outerH)}V{F(top)}", // right vertical line up

            _ => ""
        };

        if (!string.IsNullOrEmpty(pathData))
        {
            sb.Append(
                $"<path d=\"{pathData}\" stroke=\"{color}\" stroke-width=\"{F(stroke)}\" stroke-linecap=\"square\" fill=\"none\"/>");
        }
    }

    /// <summary>
    /// Renders the double cross (╬) with proper interlocking geometry.
    /// </summary>
    private static void RenderDoubleCross(StringBuilder sb, char c, double x, double y,
        double w, double h, string color, double stroke, double gap)
    {
        var left = x;
        var right = x + w;
        var top = y;
        var bottom = y + h;
        var cx = x + w / 2;
        var cy = y + h / 2;

        var outerV = cx - gap;
        var innerV = cx + gap;
        var outerH = cy - gap;
        var innerH = cy + gap;

        // Draw the interlocking double cross:
        // - Outer vertical lines run full height but skip the horizontal gap
        // - Inner vertical lines run full height but skip the horizontal gap
        // - Outer horizontal lines run full width but skip the vertical gap
        // - Inner horizontal lines run full width but skip the vertical gap
        var pathData =
            // Left vertical line (with gap for horizontal)
            $"M{F(outerV)} {F(top)}V{F(outerH)}" +
            $"M{F(outerV)} {F(innerH)}V{F(bottom)}" +
            // Right vertical line (with gap for horizontal)
            $"M{F(innerV)} {F(top)}V{F(outerH)}" +
            $"M{F(innerV)} {F(innerH)}V{F(bottom)}" +
            // Top horizontal line (with gap for vertical)
            $"M{F(left)} {F(outerH)}H{F(outerV)}" +
            $"M{F(innerV)} {F(outerH)}H{F(right)}" +
            // Bottom horizontal line (with gap for vertical)
            $"M{F(left)} {F(innerH)}H{F(outerV)}" +
            $"M{F(innerV)} {F(innerH)}H{F(right)}";

        sb.Append(
            $"<path d=\"{pathData}\" stroke=\"{color}\" stroke-width=\"{F(stroke)}\" stroke-linecap=\"square\" fill=\"none\"/>");
    }

    [Flags]
    private enum BoxSegments
    {
        None = 0,
        LightLeft = 1 << 0,
        LightRight = 1 << 1,
        LightUp = 1 << 2,
        LightDown = 1 << 3,
        HeavyLeft = 1 << 4,
        HeavyRight = 1 << 5,
        HeavyUp = 1 << 6,
        HeavyDown = 1 << 7,
        DoubleHorizontal = 1 << 8,
        DoubleVertical = 1 << 9,
        DashedHorizontal = 1 << 10,
        DashedVertical = 1 << 11,
        RoundedCorner = 1 << 12,
    }

    private static BoxSegments GetBoxDrawingSegments(char c) => c switch
    {
        // Light lines
        '\u2500' => BoxSegments.LightLeft | BoxSegments.LightRight, // ─
        '\u2502' => BoxSegments.LightUp | BoxSegments.LightDown, // │
        '\u250C' => BoxSegments.LightRight | BoxSegments.LightDown, // ┌
        '\u2510' => BoxSegments.LightLeft | BoxSegments.LightDown, // ┐
        '\u2514' => BoxSegments.LightRight | BoxSegments.LightUp, // └
        '\u2518' => BoxSegments.LightLeft | BoxSegments.LightUp, // ┘
        '\u251C' => BoxSegments.LightUp | BoxSegments.LightDown | BoxSegments.LightRight, // ├
        '\u2524' => BoxSegments.LightUp | BoxSegments.LightDown | BoxSegments.LightLeft, // ┤
        '\u252C' => BoxSegments.LightLeft | BoxSegments.LightRight | BoxSegments.LightDown, // ┬
        '\u2534' => BoxSegments.LightLeft | BoxSegments.LightRight | BoxSegments.LightUp, // ┴
        '\u253C' => BoxSegments.LightLeft | BoxSegments.LightRight | BoxSegments.LightUp | BoxSegments.LightDown, // ┼

        // Heavy lines
        '\u2501' => BoxSegments.HeavyLeft | BoxSegments.HeavyRight, // ━
        '\u2503' => BoxSegments.HeavyUp | BoxSegments.HeavyDown, // ┃
        '\u250F' => BoxSegments.HeavyRight | BoxSegments.HeavyDown, // ┏
        '\u2513' => BoxSegments.HeavyLeft | BoxSegments.HeavyDown, // ┓
        '\u2517' => BoxSegments.HeavyRight | BoxSegments.HeavyUp, // ┗
        '\u251B' => BoxSegments.HeavyLeft | BoxSegments.HeavyUp, // ┛
        '\u2523' => BoxSegments.HeavyUp | BoxSegments.HeavyDown | BoxSegments.HeavyRight, // ┣
        '\u252B' => BoxSegments.HeavyUp | BoxSegments.HeavyDown | BoxSegments.HeavyLeft, // ┫
        '\u2533' => BoxSegments.HeavyLeft | BoxSegments.HeavyRight | BoxSegments.HeavyDown, // ┳
        '\u253B' => BoxSegments.HeavyLeft | BoxSegments.HeavyRight | BoxSegments.HeavyUp, // ┻
        '\u254B' => BoxSegments.HeavyLeft | BoxSegments.HeavyRight | BoxSegments.HeavyUp | BoxSegments.HeavyDown, // ╋

        // Mixed light/heavy
        '\u250D' => BoxSegments.HeavyRight | BoxSegments.LightDown, // ┍
        '\u250E' => BoxSegments.LightRight | BoxSegments.HeavyDown, // ┎
        '\u2511' => BoxSegments.HeavyLeft | BoxSegments.LightDown, // ┑
        '\u2512' => BoxSegments.LightLeft | BoxSegments.HeavyDown, // ┒
        '\u2515' => BoxSegments.HeavyRight | BoxSegments.LightUp, // ┕
        '\u2516' => BoxSegments.LightRight | BoxSegments.HeavyUp, // ┖
        '\u2519' => BoxSegments.HeavyLeft | BoxSegments.LightUp, // ┙
        '\u251A' => BoxSegments.LightLeft | BoxSegments.HeavyUp, // ┚

        // T-junctions with mixed weights
        '\u251D' => BoxSegments.LightUp | BoxSegments.LightDown | BoxSegments.HeavyRight, // ┝
        '\u251E' => BoxSegments.HeavyUp | BoxSegments.LightDown | BoxSegments.LightRight, // ┞
        '\u251F' => BoxSegments.LightUp | BoxSegments.HeavyDown | BoxSegments.LightRight, // ┟
        '\u2520' => BoxSegments.HeavyUp | BoxSegments.HeavyDown | BoxSegments.LightRight, // ┠
        '\u2521' => BoxSegments.HeavyUp | BoxSegments.LightDown | BoxSegments.HeavyRight, // ┡
        '\u2522' => BoxSegments.LightUp | BoxSegments.HeavyDown | BoxSegments.HeavyRight, // ┢
        '\u2525' => BoxSegments.LightUp | BoxSegments.LightDown | BoxSegments.HeavyLeft, // ┥
        '\u2526' => BoxSegments.HeavyUp | BoxSegments.LightDown | BoxSegments.LightLeft, // ┦
        '\u2527' => BoxSegments.LightUp | BoxSegments.HeavyDown | BoxSegments.LightLeft, // ┧
        '\u2528' => BoxSegments.HeavyUp | BoxSegments.HeavyDown | BoxSegments.LightLeft, // ┨
        '\u2529' => BoxSegments.HeavyUp | BoxSegments.LightDown | BoxSegments.HeavyLeft, // ┩
        '\u252A' => BoxSegments.LightUp | BoxSegments.HeavyDown | BoxSegments.HeavyLeft, // ┪
        '\u252D' => BoxSegments.LightLeft | BoxSegments.HeavyRight | BoxSegments.LightDown, // ┭
        '\u252E' => BoxSegments.HeavyLeft | BoxSegments.LightRight | BoxSegments.LightDown, // ┮
        '\u252F' => BoxSegments.HeavyLeft | BoxSegments.HeavyRight | BoxSegments.LightDown, // ┯
        '\u2530' => BoxSegments.LightLeft | BoxSegments.LightRight | BoxSegments.HeavyDown, // ┰
        '\u2531' => BoxSegments.LightLeft | BoxSegments.HeavyRight | BoxSegments.HeavyDown, // ┱
        '\u2532' => BoxSegments.HeavyLeft | BoxSegments.LightRight | BoxSegments.HeavyDown, // ┲
        '\u2535' => BoxSegments.LightLeft | BoxSegments.HeavyRight | BoxSegments.LightUp, // ┵
        '\u2536' => BoxSegments.HeavyLeft | BoxSegments.LightRight | BoxSegments.LightUp, // ┶
        '\u2537' => BoxSegments.HeavyLeft | BoxSegments.HeavyRight | BoxSegments.LightUp, // ┷
        '\u2538' => BoxSegments.LightLeft | BoxSegments.LightRight | BoxSegments.HeavyUp, // ┸
        '\u2539' => BoxSegments.LightLeft | BoxSegments.HeavyRight | BoxSegments.HeavyUp, // ┹
        '\u253A' => BoxSegments.HeavyLeft | BoxSegments.LightRight | BoxSegments.HeavyUp, // ┺

        // Cross with mixed weights
        '\u253D' => BoxSegments.LightLeft | BoxSegments.HeavyRight | BoxSegments.LightUp | BoxSegments.LightDown, // ┽
        '\u253E' => BoxSegments.HeavyLeft | BoxSegments.LightRight | BoxSegments.LightUp | BoxSegments.LightDown, // ┾
        '\u253F' => BoxSegments.HeavyLeft | BoxSegments.HeavyRight | BoxSegments.LightUp | BoxSegments.LightDown, // ┿
        '\u2540' => BoxSegments.LightLeft | BoxSegments.LightRight | BoxSegments.HeavyUp | BoxSegments.LightDown, // ╀
        '\u2541' => BoxSegments.LightLeft | BoxSegments.LightRight | BoxSegments.LightUp | BoxSegments.HeavyDown, // ╁
        '\u2542' => BoxSegments.LightLeft | BoxSegments.LightRight | BoxSegments.HeavyUp | BoxSegments.HeavyDown, // ╂
        '\u2543' => BoxSegments.LightLeft | BoxSegments.HeavyRight | BoxSegments.HeavyUp | BoxSegments.LightDown, // ╃
        '\u2544' => BoxSegments.HeavyLeft | BoxSegments.LightRight | BoxSegments.HeavyUp | BoxSegments.LightDown, // ╄
        '\u2545' => BoxSegments.LightLeft | BoxSegments.HeavyRight | BoxSegments.LightUp | BoxSegments.HeavyDown, // ╅
        '\u2546' => BoxSegments.HeavyLeft | BoxSegments.LightRight | BoxSegments.LightUp | BoxSegments.HeavyDown, // ╆
        '\u2547' => BoxSegments.HeavyLeft | BoxSegments.HeavyRight | BoxSegments.HeavyUp | BoxSegments.LightDown, // ╇
        '\u2548' => BoxSegments.HeavyLeft | BoxSegments.HeavyRight | BoxSegments.LightUp | BoxSegments.HeavyDown, // ╈
        '\u2549' => BoxSegments.LightLeft | BoxSegments.HeavyRight | BoxSegments.HeavyUp | BoxSegments.HeavyDown, // ╉
        '\u254A' => BoxSegments.HeavyLeft | BoxSegments.LightRight | BoxSegments.HeavyUp | BoxSegments.HeavyDown, // ╊

        // Dashed lines
        '\u2504' => BoxSegments.DashedHorizontal, // ┄ (triple dash)
        '\u2505' => BoxSegments.DashedHorizontal, // ┅ (triple dash heavy)
        '\u2506' => BoxSegments.DashedVertical, // ┆ (triple dash)
        '\u2507' => BoxSegments.DashedVertical, // ┇ (triple dash heavy)
        '\u2508' => BoxSegments.DashedHorizontal, // ┈ (quadruple dash)
        '\u2509' => BoxSegments.DashedHorizontal, // ┉ (quadruple dash heavy)
        '\u250A' => BoxSegments.DashedVertical, // ┊ (quadruple dash)
        '\u250B' => BoxSegments.DashedVertical, // ┋ (quadruple dash heavy)

        // Double lines
        '\u2550' => BoxSegments.DoubleHorizontal, // ═
        '\u2551' => BoxSegments.DoubleVertical, // ║
        '\u2554' => BoxSegments.DoubleHorizontal | BoxSegments.DoubleVertical, // ╔ (handled specially)
        '\u2557' => BoxSegments.DoubleHorizontal | BoxSegments.DoubleVertical, // ╗
        '\u255A' => BoxSegments.DoubleHorizontal | BoxSegments.DoubleVertical, // ╚
        '\u255D' => BoxSegments.DoubleHorizontal | BoxSegments.DoubleVertical, // ╝
        '\u2560' => BoxSegments.DoubleVertical, // ╠ (handled specially by RenderDoubleTJunction)
        '\u2563' => BoxSegments.DoubleVertical, // ╣ (handled specially by RenderDoubleTJunction)
        '\u2566' => BoxSegments.DoubleHorizontal, // ╦ (handled specially by RenderDoubleTJunction)
        '\u2569' => BoxSegments.DoubleHorizontal, // ╩ (handled specially by RenderDoubleTJunction)
        '\u256C' => BoxSegments.DoubleHorizontal | BoxSegments.DoubleVertical, // ╬

        // Single/double combinations
        '\u2552' => BoxSegments.LightDown | BoxSegments.DoubleHorizontal, // ╒
        '\u2553' => BoxSegments.DoubleVertical | BoxSegments.LightRight, // ╓
        '\u2555' => BoxSegments.LightDown | BoxSegments.DoubleHorizontal, // ╕
        '\u2556' => BoxSegments.DoubleVertical | BoxSegments.LightLeft, // ╖
        '\u2558' => BoxSegments.LightUp | BoxSegments.DoubleHorizontal, // ╘
        '\u2559' => BoxSegments.DoubleVertical | BoxSegments.LightRight, // ╙
        '\u255B' => BoxSegments.LightUp | BoxSegments.DoubleHorizontal, // ╛
        '\u255C' => BoxSegments.DoubleVertical | BoxSegments.LightLeft, // ╜
        '\u255E' => BoxSegments.LightUp | BoxSegments.LightDown | BoxSegments.DoubleHorizontal, // ╞
        '\u255F' => BoxSegments.DoubleVertical | BoxSegments.LightRight, // ╟
        '\u2561' => BoxSegments.LightUp | BoxSegments.LightDown | BoxSegments.DoubleHorizontal, // ╡
        '\u2562' => BoxSegments.DoubleVertical | BoxSegments.LightLeft, // ╢
        '\u2564' => BoxSegments.DoubleHorizontal | BoxSegments.LightDown, // ╤
        '\u2565' => BoxSegments.LightLeft | BoxSegments.LightRight | BoxSegments.DoubleVertical, // ╥
        '\u2567' => BoxSegments.DoubleHorizontal | BoxSegments.LightUp, // ╧
        '\u2568' => BoxSegments.LightLeft | BoxSegments.LightRight | BoxSegments.DoubleVertical, // ╨
        '\u256A' => BoxSegments.DoubleHorizontal | BoxSegments.LightUp | BoxSegments.LightDown, // ╪
        '\u256B' => BoxSegments.LightLeft | BoxSegments.LightRight | BoxSegments.DoubleVertical, // ╫

        // Rounded corners
        '\u256D' => BoxSegments.RoundedCorner, // ╭
        '\u256E' => BoxSegments.RoundedCorner, // ╮
        '\u256F' => BoxSegments.RoundedCorner, // ╯
        '\u2570' => BoxSegments.RoundedCorner, // ╰

        // Diagonal lines (approximated with lines)
        '\u2571' => BoxSegments.LightLeft | BoxSegments.LightRight, // ╱ (diagonal, draw as special)
        '\u2572' => BoxSegments.LightLeft | BoxSegments.LightRight, // ╲
        '\u2573' => BoxSegments.LightLeft | BoxSegments.LightRight, // ╳

        // Half lines
        '\u2574' => BoxSegments.LightLeft, // ╴ left
        '\u2575' => BoxSegments.LightUp, // ╵ up
        '\u2576' => BoxSegments.LightRight, // ╶ right
        '\u2577' => BoxSegments.LightDown, // ╷ down
        '\u2578' => BoxSegments.HeavyLeft, // ╸ heavy left
        '\u2579' => BoxSegments.HeavyUp, // ╹ heavy up
        '\u257A' => BoxSegments.HeavyRight, // ╺ heavy right
        '\u257B' => BoxSegments.HeavyDown, // ╻ heavy down
        '\u257C' => BoxSegments.LightLeft | BoxSegments.HeavyRight, // ╼ light left heavy right
        '\u257D' => BoxSegments.LightUp | BoxSegments.HeavyDown, // ╽ light up heavy down
        '\u257E' => BoxSegments.HeavyLeft | BoxSegments.LightRight, // ╾ heavy left light right
        '\u257F' => BoxSegments.HeavyUp | BoxSegments.LightDown, // ╿ heavy up light down

        _ => BoxSegments.None
    };

    private static void RenderBlockElement(StringBuilder sb, char c, double x, double y,
        double w, double h, string color)
    {
        // Block elements are simple rectangles or patterns
        var (rx, ry, rw, rh, pattern) = GetBlockElementRect(c, w, h);

        if (pattern != null)
        {
            // Shade pattern - use pattern fill
            sb.Append(
                $"<rect x=\"{F(x + rx)}\" y=\"{F(y + ry)}\" width=\"{F(rw)}\" height=\"{F(rh)}\" fill=\"url(#{pattern})\" style=\"--shade-color:{color}\"/>");
        }
        else if (rw > 0 && rh > 0)
        {
            // Solid block
            sb.Append(
                $"<rect x=\"{F(x + rx)}\" y=\"{F(y + ry)}\" width=\"{F(rw)}\" height=\"{F(rh)}\" fill=\"{color}\"/>");
        }
    }

    private static (double x, double y, double w, double h, string? pattern) GetBlockElementRect(char c, double cellW,
        double cellH)
    {
        return c switch
        {
            '\u2580' => (0, 0, cellW, cellH / 2, null), // ▀ upper half
            '\u2581' => (0, cellH * 7 / 8, cellW, cellH / 8, null), // ▁ lower 1/8
            '\u2582' => (0, cellH * 3 / 4, cellW, cellH / 4, null), // ▂ lower 1/4
            '\u2583' => (0, cellH * 5 / 8, cellW, cellH * 3 / 8, null), // ▃ lower 3/8
            '\u2584' => (0, cellH / 2, cellW, cellH / 2, null), // ▄ lower half
            '\u2585' => (0, cellH * 3 / 8, cellW, cellH * 5 / 8, null), // ▅ lower 5/8
            '\u2586' => (0, cellH / 4, cellW, cellH * 3 / 4, null), // ▆ lower 3/4
            '\u2587' => (0, cellH / 8, cellW, cellH * 7 / 8, null), // ▇ lower 7/8
            '\u2588' => (0, 0, cellW, cellH, null), // █ full block
            '\u2589' => (0, 0, cellW * 7 / 8, cellH, null), // ▉ left 7/8
            '\u258A' => (0, 0, cellW * 3 / 4, cellH, null), // ▊ left 3/4
            '\u258B' => (0, 0, cellW * 5 / 8, cellH, null), // ▋ left 5/8
            '\u258C' => (0, 0, cellW / 2, cellH, null), // ▌ left half
            '\u258D' => (0, 0, cellW * 3 / 8, cellH, null), // ▍ left 3/8
            '\u258E' => (0, 0, cellW / 4, cellH, null), // ▎ left 1/4
            '\u258F' => (0, 0, cellW / 8, cellH, null), // ▏ left 1/8
            '\u2590' => (cellW / 2, 0, cellW / 2, cellH, null), // ▐ right half
            '\u2591' => (0, 0, cellW, cellH, "shade-light"), // ░ light shade
            '\u2592' => (0, 0, cellW, cellH, "shade-medium"), // ▒ medium shade
            '\u2593' => (0, 0, cellW, cellH, "shade-dark"), // ▓ dark shade
            '\u2594' => (0, 0, cellW, cellH / 8, null), // ▔ upper 1/8
            '\u2595' => (cellW * 7 / 8, 0, cellW / 8, cellH, null), // ▕ right 1/8
            '\u2596' => (0, cellH / 2, cellW / 2, cellH / 2, null), // ▖ quadrant lower left
            '\u2597' => (cellW / 2, cellH / 2, cellW / 2, cellH / 2, null), // ▗ quadrant lower right
            '\u2598' => (0, 0, cellW / 2, cellH / 2, null), // ▘ quadrant upper left
            '\u2599' => (0, 0, cellW, cellH, null), // ▙ quadrant upper left and lower left and lower right (special)
            '\u259A' => (0, 0, cellW, cellH, null), // ▚ quadrant upper left and lower right (special)
            '\u259B' => (0, 0, cellW, cellH, null), // ▛ quadrant upper left and upper right and lower left (special)
            '\u259C' => (0, 0, cellW, cellH, null), // ▜ quadrant upper left and upper right and lower right (special)
            '\u259D' => (cellW / 2, 0, cellW / 2, cellH / 2, null), // ▝ quadrant upper right
            '\u259E' => (0, 0, cellW, cellH, null), // ▞ quadrant upper right and lower left (special)
            '\u259F' => (0, 0, cellW, cellH, null), // ▟ quadrant upper right and lower left and lower right (special)
            _ => (0, 0, 0, 0, null)
        };
    }

    #region Powerline Characters

    private static void RenderPowerline(StringBuilder sb, char c, double x, double y,
        double w, double h, string color)
    {
        var path = GetPowerlinePath(c, x, y, w, h);
        if (!string.IsNullOrEmpty(path))
        {
            // Most powerline symbols are filled shapes
            sb.Append($"<path d=\"{path}\" fill=\"{color}\"/>");
        }
    }

    private static string GetPowerlinePath(char c, double x, double y, double w, double h)
    {
        var cx = x + w / 2;
        var cy = y + h / 2;
        var right = x + w;
        var bottom = y + h;

        return c switch
        {
            // Triangle separators
            '\uE0B0' => $"M{F(x)} {F(y)}L{F(right)} {F(cy)}L{F(x)} {F(bottom)}Z", //  right-pointing solid
            '\uE0B1' => $"M{F(x)} {F(y)}L{F(right)} {F(cy)}L{F(x)} {F(bottom)}", //  right-pointing thin (stroke only)
            '\uE0B2' => $"M{F(right)} {F(y)}L{F(x)} {F(cy)}L{F(right)} {F(bottom)}Z", //  left-pointing solid
            '\uE0B3' => $"M{F(right)} {F(y)}L{F(x)} {F(cy)}L{F(right)} {F(bottom)}", //  left-pointing thin

            // Semi-circle separators
            '\uE0B4' => $"M{F(x)} {F(y)}Q{F(right)} {F(cy)} {F(x)} {F(bottom)}Z", //  right semi-circle
            '\uE0B5' => $"M{F(x)} {F(y)}Q{F(right)} {F(cy)} {F(x)} {F(bottom)}", // thin variant
            '\uE0B6' => $"M{F(right)} {F(y)}Q{F(x)} {F(cy)} {F(right)} {F(bottom)}Z", //  left semi-circle
            '\uE0B7' => $"M{F(right)} {F(y)}Q{F(x)} {F(cy)} {F(right)} {F(bottom)}", // thin variant

            // Lower triangles
            '\uE0B8' => $"M{F(x)} {F(bottom)}L{F(right)} {F(bottom)}L{F(x)} {F(y)}Z", // lower left triangle
            '\uE0B9' => $"M{F(x)} {F(bottom)}L{F(right)} {F(bottom)}L{F(x)} {F(y)}", // thin
            '\uE0BA' => $"M{F(right)} {F(bottom)}L{F(x)} {F(bottom)}L{F(right)} {F(y)}Z", // lower right triangle
            '\uE0BB' => $"M{F(right)} {F(bottom)}L{F(x)} {F(bottom)}L{F(right)} {F(y)}", // thin

            // Upper triangles
            '\uE0BC' => $"M{F(x)} {F(y)}L{F(right)} {F(y)}L{F(x)} {F(bottom)}Z", // upper left
            '\uE0BD' => $"M{F(x)} {F(y)}L{F(right)} {F(y)}L{F(x)} {F(bottom)}", // thin
            '\uE0BE' => $"M{F(right)} {F(y)}L{F(x)} {F(y)}L{F(right)} {F(bottom)}Z", // upper right
            '\uE0BF' => $"M{F(right)} {F(y)}L{F(x)} {F(y)}L{F(right)} {F(bottom)}", // thin

            // Git branch symbol (simplified)
            '\uE0A0' => BuildBranchSymbol(x, y, w, h),

            // Line number symbol (LN)
            '\uE0A1' => "", // Too complex for path, skip

            // Lock symbol (simplified)
            '\uE0A2' => BuildLockSymbol(x, y, w, h),

            // Column number
            '\uE0A3' => "", // Skip

            // Additional separators
            '\uE0C0' => $"M{F(x)} {F(y)}L{F(cx)} {F(cy)}L{F(x)} {F(bottom)}L{F(right)} {F(bottom)}L{F(right)} {F(y)}Z", // flame right
            '\uE0C1' => $"M{F(x)} {F(y)}L{F(cx)} {F(cy)}L{F(x)} {F(bottom)}", // flame right thin
            '\uE0C2' => $"M{F(right)} {F(y)}L{F(cx)} {F(cy)}L{F(right)} {F(bottom)}L{F(x)} {F(bottom)}L{F(x)} {F(y)}Z", // flame left
            '\uE0C3' => $"M{F(right)} {F(y)}L{F(cx)} {F(cy)}L{F(right)} {F(bottom)}", // flame left thin

            // Pixelated separators (simplified to triangles)
            '\uE0C4' => $"M{F(x)} {F(y)}L{F(right)} {F(cy)}L{F(x)} {F(bottom)}Z",
            '\uE0C5' => $"M{F(x)} {F(y)}L{F(right)} {F(cy)}L{F(x)} {F(bottom)}",
            '\uE0C6' => $"M{F(right)} {F(y)}L{F(x)} {F(cy)}L{F(right)} {F(bottom)}Z",
            '\uE0C7' => $"M{F(right)} {F(y)}L{F(x)} {F(cy)}L{F(right)} {F(bottom)}",

            // Hexagon separators
            '\uE0CC' => BuildHexagonRight(x, y, w, h),
            '\uE0CD' => BuildHexagonRight(x, y, w, h), // thin
            '\uE0CE' => BuildHexagonLeft(x, y, w, h),
            '\uE0CF' => BuildHexagonLeft(x, y, w, h), // thin

            // Rounded separators
            '\uE0D0' => $"M{F(x)} {F(y)}C{F(right)} {F(y)} {F(right)} {F(bottom)} {F(x)} {F(bottom)}Z",
            '\uE0D1' => $"M{F(x)} {F(y)}C{F(right)} {F(y)} {F(right)} {F(bottom)} {F(x)} {F(bottom)}",

            _ => ""
        };
    }

    private static string BuildBranchSymbol(double x, double y, double w, double h)
    {
        // Simplified branch symbol: a Y shape
        var cx = x + w / 2;
        var cy = y + h / 2;
        var r = Math.Min(w, h) * 0.15;
        return
            $"M{F(cx)} {F(y + h * 0.2)}V{F(cy)}M{F(cx)} {F(cy)}L{F(x + w * 0.25)} {F(y + h * 0.7)}M{F(cx)} {F(cy)}L{F(x + w * 0.75)} {F(y + h * 0.7)}" +
            $"M{F(cx + r)} {F(y + h * 0.2)}A{F(r)} {F(r)} 0 1 0 {F(cx - r)} {F(y + h * 0.2)}A{F(r)} {F(r)} 0 1 0 {F(cx + r)} {F(y + h * 0.2)}" +
            $"M{F(x + w * 0.25 + r)} {F(y + h * 0.7)}A{F(r)} {F(r)} 0 1 0 {F(x + w * 0.25 - r)} {F(y + h * 0.7)}A{F(r)} {F(r)} 0 1 0 {F(x + w * 0.25 + r)} {F(y + h * 0.7)}" +
            $"M{F(x + w * 0.75 + r)} {F(y + h * 0.7)}A{F(r)} {F(r)} 0 1 0 {F(x + w * 0.75 - r)} {F(y + h * 0.7)}A{F(r)} {F(r)} 0 1 0 {F(x + w * 0.75 + r)} {F(y + h * 0.7)}";
    }

    private static string BuildLockSymbol(double x, double y, double w, double h)
    {
        // Simplified lock: rectangle with rounded top arc
        var padX = w * 0.2;
        var padY = h * 0.15;
        var bodyTop = y + h * 0.45;
        var bodyW = w - 2 * padX;
        var bodyH = h * 0.4;
        var shackleW = bodyW * 0.6;
        var shackleH = h * 0.35;
        var shackleX = x + padX + (bodyW - shackleW) / 2;

        return $"M{F(x + padX)} {F(bodyTop)}H{F(x + padX + bodyW)}V{F(bodyTop + bodyH)}H{F(x + padX)}Z" +
               $"M{F(shackleX)} {F(bodyTop)}V{F(bodyTop - shackleH * 0.5)}A{F(shackleW / 2)} {F(shackleH * 0.5)} 0 1 1 {F(shackleX + shackleW)} {F(bodyTop - shackleH * 0.5)}V{F(bodyTop)}";
    }

    private static string BuildHexagonRight(double x, double y, double w, double h)
    {
        var indent = w * 0.3;
        return
            $"M{F(x)} {F(y)}L{F(x + w - indent)} {F(y)}L{F(x + w)} {F(y + h / 2)}L{F(x + w - indent)} {F(y + h)}L{F(x)} {F(y + h)}Z";
    }

    private static string BuildHexagonLeft(double x, double y, double w, double h)
    {
        var indent = w * 0.3;
        return
            $"M{F(x + w)} {F(y)}L{F(x + indent)} {F(y)}L{F(x)} {F(y + h / 2)}L{F(x + indent)} {F(y + h)}L{F(x + w)} {F(y + h)}Z";
    }


    private static string F(double value)
    {
        if (Math.Abs(value % 1) < 0.001)
            return ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture);
        return value.ToString("F2", CultureInfo.InvariantCulture);
    }

    #endregion
}