namespace VcrSharp.Core.Parsing.Ast;

/// <summary>
/// Represents a Show command that resumes frame capture.
/// </summary>
public class ShowCommand : ICommand
{
    public async Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        // Resume capturing frames
        context.State.IsCapturing = true;
        await Task.CompletedTask;
    }

    public override string ToString() => "Show";
}