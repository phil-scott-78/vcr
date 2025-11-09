using VcrSharp.Core.Logging;

namespace VcrSharp.Core.Parsing.Ast;

/// <summary>
/// Represents a key press command (Enter, Tab, arrows, etc.).
/// Example: Enter, Backspace 5, Left@100ms 2
/// </summary>
public class KeyCommand(string keyName, int repeatCount = 1, TimeSpan? speed = null)
    : ICommand
{
    public string KeyName { get; } = keyName;
    public int RepeatCount { get; } = Math.Max(1, repeatCount);
    public TimeSpan? Speed { get; } = speed;

    public async Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        var page = context.GetTerminalPage();

        // Determine typing speed (use override if specified, otherwise use session default)
        var typingSpeed = Speed ?? context.Options.TypingSpeed;
        var delayMs = (int)typingSpeed.TotalMilliseconds;

        // Map key name to Playwright key
        var mappedKey = Helpers.KeyMapper.MapKey(KeyName);
        VcrLogger.Logger.Debug("KeyCommand: Pressing key '{KeyName}' (mapped to '{MappedKey}') {RepeatCount} time(s) with {DelayMs}ms delay",
            KeyName, mappedKey, RepeatCount, delayMs);

        // Press key RepeatCount times with delay between presses
        for (var i = 0; i < RepeatCount; i++)
        {
            if (i > 0 && delayMs > 0)
            {
                await Task.Delay(delayMs, cancellationToken);
            }

            VcrLogger.Logger.Debug("KeyCommand: Sending key press #{Iteration}: '{MappedKey}'", i + 1, mappedKey);
            await page.PressKeyAsync(mappedKey);
            VcrLogger.Logger.Debug("KeyCommand: Key press #{Iteration} completed", i + 1);
        }

        VcrLogger.Logger.Debug("KeyCommand: All {RepeatCount} key presses completed for '{KeyName}'", RepeatCount, KeyName);
    }

    public override string ToString()
    {
        var speedStr = Speed.HasValue ? $"@{Speed.Value.TotalMilliseconds}ms" : "";
        var countStr = RepeatCount > 1 ? $" {RepeatCount}" : "";
        return $"{KeyName}{speedStr}{countStr}";
    }
}