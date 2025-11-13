namespace VcrSharp.Core.Parsing.Ast;

/// <summary>
/// Represents an Env command that sets an environment variable.
/// Example: Env USER "alice"
/// </summary>
public class EnvCommand : ICommand
{
    public string Key { get; }
    public string Value { get; }

    public EnvCommand(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        Key = key;
        Value = value;
    }

    public Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        // Environment variables are set before recording starts
        return Task.CompletedTask;
    }

    public override string ToString() => $"Env {Key} \"{Value}\"";
}