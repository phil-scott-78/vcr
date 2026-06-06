namespace VcrSharp.Core.Parsing.Ast;

/// <summary>
/// Represents a Run command: sugar for "type a command at the live prompt, press Enter,
/// then wait for the output to settle". Example: <c>Run "./example Alice"</c>.
/// <para>
/// <see cref="Config.PresetResolver"/> desugars Run into <see cref="TypeCommand"/> +
/// <see cref="KeyCommand"/> (Enter) + <see cref="WaitCommand"/> before recording, so the
/// frame-capture loop only ever sees the primitive commands. The <see cref="ExecuteAsync"/>
/// fallback (type + Enter) keeps the command self-contained if it is ever executed directly.
/// </para>
/// </summary>
public class RunCommand(string text, int lineNumber = 0) : ICommand
{
    public string Text { get; } = text;
    public int LineNumber { get; } = lineNumber;

    public async Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        var page = context.Page;
        var delayMs = (int)context.Options.TypingSpeed.TotalMilliseconds;

        await page.TypeAsync(Text, delayMs);
        await page.PressKeyAsync(Helpers.KeyMapper.MapKey("Enter"));
    }

    public override string ToString() => $"Run \"{Text}\"";
}
