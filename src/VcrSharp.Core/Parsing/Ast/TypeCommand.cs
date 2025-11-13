namespace VcrSharp.Core.Parsing.Ast;

/// <summary>
/// Represents a Type command that simulates typing text.
/// Example: Type "hello world", Type@500ms "slow typing"
/// </summary>
public class TypeCommand(string text, TimeSpan? speed = null) : ICommand
{
    public string Text { get; } = text;
    public TimeSpan? Speed { get; } = speed;

    public async Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        var page = context.Page;

        // Determine typing speed (use override if specified, otherwise use session default)
        var typingSpeed = Speed ?? context.Options.TypingSpeed;
        var delayMs = (int)typingSpeed.TotalMilliseconds;

        // Type text character-by-character with delay
        await page.TypeAsync(Text, delayMs);
    }

    public override string ToString()
    {
        var speedStr = Speed.HasValue ? $"@{Speed.Value.TotalMilliseconds}ms" : "";
        return $"Type{speedStr} \"{Text}\"";
    }
}