namespace VcrSharp.Core.Parsing.Ast;

/// <summary>
/// Represents a Source command that includes another tape file.
/// Example: Source common.tape
/// </summary>
public class SourceCommand(string filePath) : ICommand
{
    public string FilePath { get; } = filePath;

    public Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        // Source files are inlined during parsing
        return Task.CompletedTask;
    }

    public override string ToString() => $"Source {FilePath}";
}