namespace VcrSharp.Core.Parsing.Ast;

/// <summary>
/// Represents an Output command specifying an output file.
/// Example: Output demo.gif, Output recording.mp4
/// </summary>
public class OutputCommand(string filePath) : ICommand
{
    public string FilePath { get; } = filePath;

    public Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        // Output files are collected before recording starts
        return Task.CompletedTask;
    }

    public override string ToString() => $"Output {FilePath}";
}