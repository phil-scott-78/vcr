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
        // Optionally wait for the terminal to settle so a Screenshot taken right after an Exec
        // command captures the finished output instead of an empty/partial screen.
        if (context.Options.ScreenshotWaitForInactivity)
        {
            await context.FrameCapture.WaitForBufferStableAsync(
                context.Options.ScreenshotInactivityTimeout,
                context.Options.MaxWaitForInactivity,
                cancellationToken);
        }

        // Delegate to frame capture - format detection happens in Infrastructure layer
        await context.FrameCapture.CaptureScreenshotAsync(FilePath);
    }

    public override string ToString() => $"Screenshot {FilePath}";
}