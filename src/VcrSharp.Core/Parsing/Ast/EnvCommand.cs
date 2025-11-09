namespace VcrSharp.Core.Parsing.Ast;

/// <summary>
/// Represents an Env command that sets an environment variable.
/// Example: Env USER "alice"
/// </summary>
public class EnvCommand(string key, string value) : ICommand
{
    public string Key { get; } = key ?? throw new ArgumentNullException(nameof(key));
    public string Value { get; } = value ?? throw new ArgumentNullException(nameof(value));

    public Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        // Environment variables are set before recording starts
        return Task.CompletedTask;
    }

    public override string ToString() => $"Env {Key} \"{Value}\"";
}