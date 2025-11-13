namespace VcrSharp.Core.Parsing.Ast;

/// <summary>
/// Represents an Exec command that executes a real shell command (VcrSharp-specific).
/// Example: Exec "npm install", Exec "git status"
/// </summary>
public class ExecCommand : ICommand
{
    public string Command { get; }

    public ExecCommand(string command)
    {
        ArgumentNullException.ThrowIfNull(command);
        Command = command;
    }

    public Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        // Exec commands are executed at ttyd startup via startup script.
        // They run in the background while recording proceeds.
        // No waiting or synchronization needed - recording starts immediately.
        return Task.CompletedTask;
    }

    public override string ToString() => $"Exec \"{Command}\"";
}