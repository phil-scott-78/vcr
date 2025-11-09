namespace VcrSharp.Core.Parsing.Ast;

/// <summary>
/// Represents a Sleep command that pauses execution.
/// Example: Sleep 1s, Sleep 500ms, Sleep 2m
/// </summary>
public class SleepCommand(TimeSpan duration) : ICommand
{
    public TimeSpan Duration { get; } = duration;

    public async Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        await Task.Delay(Duration, cancellationToken);
    }

    public override string ToString() => $"Sleep {Duration.TotalSeconds}s";
}