namespace VcrSharp.Core.Parsing.Ast;

/// <summary>
/// Represents a Screenshot command that captures a single frame.
/// Supports both PNG (raster) and SVG (vector) output formats.
/// Format is auto-detected from file extension.
/// Examples:
///   Screenshot output.png  (PNG format)
///   Screenshot output.svg  (SVG format)
/// </summary>
public class ScreenshotCommand(string filePath) : ICommand
{
    public string FilePath { get; } = filePath;

    public async Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        // Delegate to frame capture - format detection happens in Infrastructure layer
        await context.FrameCapture.CaptureScreenshotAsync(FilePath);
    }

    public override string ToString() => $"Screenshot {FilePath}";
}