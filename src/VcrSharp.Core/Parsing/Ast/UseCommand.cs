namespace VcrSharp.Core.Parsing.Ast;

/// <summary>
/// Represents a Use command that pulls a named preset from a discovered <c>vcr.toml</c>.
/// Example: <c>Use doc</c>
/// <para>
/// Use commands are config, not actions: they are expanded into their preset's Set
/// commands (and an optional derived Output) by <see cref="Config.PresetResolver"/> before
/// recording starts, so this command never reaches the execution loop.
/// </para>
/// </summary>
public class UseCommand(string presetName, int lineNumber = 0) : ICommand
{
    public string PresetName { get; } = presetName;
    public int LineNumber { get; } = lineNumber;

    public Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        // Resolved into preset Set commands before execution; here for interface compliance.
        return Task.CompletedTask;
    }

    public override string ToString() => $"Use {PresetName}";
}
