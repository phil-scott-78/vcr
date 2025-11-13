using Spectre.Console;
using Spectre.Console.Rendering;

namespace VcrSharp.Cli.Helpers;

/// <summary>
/// A widget that wraps any <see cref="IRenderable"/> and applies a gradient color scheme.
/// </summary>
public sealed class Gradient : Renderable
{
    private readonly IRenderable _child;

    /// <summary>
    /// Gets the colors used in the gradient.
    /// </summary>
    private IReadOnlyList<Color> Colors { get; }

    /// <summary>
    /// Gets the direction of the gradient.
    /// </summary>
    private GradientDirection Direction { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Gradient"/> class.
    /// </summary>
    /// <param name="child">The renderable to apply the gradient to.</param>
    /// <param name="colors">The colors to use in the gradient (minimum 2 required).</param>
    /// <param name="direction">The direction of the gradient.</param>
    public Gradient(IRenderable child, IEnumerable<Color> colors,
        GradientDirection direction = GradientDirection.LeftToRight)
    {
        ArgumentNullException.ThrowIfNull(child);
        ArgumentNullException.ThrowIfNull(colors);

        _child = child;


        var colorArray = colors.ToArray();
        if (colorArray.Length < 2)
        {
            throw new ArgumentException("At least two colors are required for a gradient.", nameof(colors));
        }

        Colors = colorArray;
        Direction = direction;
    }

    /// <inheritdoc/>
    protected override Measurement Measure(RenderOptions options, int maxWidth)
    {
        return _child.Measure(options, maxWidth);
    }

    /// <inheritdoc/>
    protected override IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
    {
        // Render the child
        var childSegments = _child.Render(options, maxWidth);
        var segments = childSegments as Segment[] ?? childSegments.ToArray();
        var lines = Segment.SplitLines(segments);

        if (lines.Count == 0)
        {
            return [];
        }

        // Calculate grid dimensions
        var totalHeight = lines.Count;
        var totalWidth = lines.Max(line => line.CellCount());

        if (totalWidth == 0 || totalHeight == 0)
        {
            return segments;
        }

        var result = new List<Segment>();

        // Process each line
        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            var line = lines[lineIndex];
            var currentColumn = 0;

            foreach (var segment in line)
            {
                // Pass through control codes and line breaks unchanged
                if (segment.IsControlCode || segment.IsLineBreak)
                {
                    result.Add(segment);
                    continue;
                }

                // Split segment into individual characters for per-character coloring
                foreach (var ch in segment.Text)
                {
                    // Calculate gradient factor for this character's position
                    var factor = CalculateGradientFactor(lineIndex + 0.5, currentColumn + 0.5, totalHeight, totalWidth);
                    var gradientColor = InterpolateColor(factor);

                    // Create new style with gradient color, preserving other properties
                    var newStyle = new Style(
                        foreground: gradientColor,
                        background: segment.Style.Background,
                        decoration: segment.Style.Decoration,
                        link: segment.Style.Link);

                    result.Add(new Segment(ch.ToString(), newStyle));
                    currentColumn++;
                }
            }

            result.Add(Segment.LineBreak);
        }

        return result;
    }

    private double CalculateGradientFactor(double row, double column, int totalHeight, int totalWidth)
    {
        // Normalize row and column to 0.0-1.0 range
        var normalizedRow = totalHeight > 1 ? row / totalHeight : 0.5;
        var normalizedColumn = totalWidth > 1 ? column / totalWidth : 0.5;

        return Direction switch
        {
            GradientDirection.LeftToRight => normalizedColumn,
            GradientDirection.RightToLeft => 1.0 - normalizedColumn,
            GradientDirection.TopToBottom => normalizedRow,
            GradientDirection.BottomToTop => 1.0 - normalizedRow,
            GradientDirection.TopLeftToBottomRight => (normalizedRow + normalizedColumn) / 2.0,
            GradientDirection.TopRightToBottomLeft => (normalizedRow + (1.0 - normalizedColumn)) / 2.0,
            GradientDirection.BottomLeftToTopRight => (1.0 - normalizedRow + normalizedColumn) / 2.0,
            GradientDirection.BottomRightToTopLeft => (1.0 - normalizedRow + (1.0 - normalizedColumn)) / 2.0,
            _ => normalizedColumn,
        };
    }

    private Color InterpolateColor(double factor)
    {
        // Clamp factor to 0.0-1.0
        factor = Math.Max(0.0, Math.Min(1.0, factor));

        // If we only have two colors, use the built-in Blend method
        if (Colors.Count == 2)
        {
            return Colors[0].Blend(Colors[1], (float)factor);
        }

        // For multiple colors, determine which two colors to blend between
        var segmentCount = Colors.Count - 1;
        var scaledFactor = factor * segmentCount;
        var segmentIndex = (int)Math.Floor(scaledFactor);

        // Handle edge case where factor is exactly 1.0
        if (segmentIndex >= segmentCount)
        {
            return Colors[^1];
        }

        // Calculate the blend factor within this segment
        var segmentFactor = (float)(scaledFactor - segmentIndex);

        return Colors[segmentIndex].Blend(Colors[segmentIndex + 1], segmentFactor);
    }
}

/// <summary>
/// Represents the direction of a gradient.
/// </summary>
public enum GradientDirection
{
    /// <summary>
    /// Gradient flows from left to right.
    /// </summary>
    LeftToRight,

    /// <summary>
    /// Gradient flows from right to left.
    /// </summary>
    RightToLeft,

    /// <summary>
    /// Gradient flows from top to bottom.
    /// </summary>
    TopToBottom,

    /// <summary>
    /// Gradient flows from bottom to top.
    /// </summary>
    BottomToTop,

    /// <summary>
    /// Gradient flows from top-left to bottom-right.
    /// </summary>
    TopLeftToBottomRight,

    /// <summary>
    /// Gradient flows from top-right to bottom-left.
    /// </summary>
    TopRightToBottomLeft,

    /// <summary>
    /// Gradient flows from bottom-left to top-right.
    /// </summary>
    BottomLeftToTopRight,

    /// <summary>
    /// Gradient flows from bottom-right to top-left.
    /// </summary>
    BottomRightToTopLeft,
}