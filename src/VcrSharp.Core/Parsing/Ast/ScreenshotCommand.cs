namespace VcrSharp.Core.Parsing.Ast;

/// <summary>
/// Represents a Screenshot command that captures a single frame.
/// Example: Screenshot output.png
/// </summary>
public class ScreenshotCommand(string filePath) : ICommand
{
    public string FilePath { get; } = filePath;

    public async Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        var frameCapture = context.FrameCapture;
        await frameCapture.CaptureScreenshotAsync(FilePath);
    }

    public override string ToString() => $"Screenshot {FilePath}";
}