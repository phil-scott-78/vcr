namespace VcrSharp.Core.Parsing.Ast;

/// <summary>
/// Represents a Hide command that stops frame capture.
/// </summary>
public class HideCommand : ICommand
{
    public async Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        // Stop capturing frames (but continue command execution)
        context.State.IsCapturing = false;
        await Task.CompletedTask;
    }

    public override string ToString() => "Hide";
}