namespace VcrSharp.Core.Parsing.Ast;

/// <summary>
/// Represents a Set command that configures a session setting.
/// Example: Set FontSize 32, Set Theme "Dracula"
/// </summary>
public class SetCommand(string settingName, object value, int lineNumber = 0) : ICommand
{
    public string SettingName { get; } = settingName;
    public object Value { get; } = value;
    public int LineNumber { get; } = lineNumber;

    public Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        // Settings are applied before recording starts, so this is handled by the parser
        // This method is here for interface compliance
        return Task.CompletedTask;
    }

    public override string ToString() => $"Set {SettingName} = {Value}";
}