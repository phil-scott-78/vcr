namespace VcrSharp.Core.Parsing.Ast;

/// <summary>
/// Represents a Require command that specifies a required program.
/// Example: Require npm, Require git
/// </summary>
public class RequireCommand(string programName) : ICommand
{
    public string ProgramName { get; } = programName;

    public Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        // Requirements are validated before recording starts
        return Task.CompletedTask;
    }

    public override string ToString() => $"Require {ProgramName}";
}