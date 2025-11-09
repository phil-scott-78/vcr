namespace VcrSharp.Core.Parsing.Ast;

/// <summary>
/// Represents a Copy command that copies text to clipboard.
/// Example: Copy "hello world"
/// </summary>
public class CopyCommand(string text) : ICommand
{
    public string Text { get; } = text ?? throw new ArgumentNullException(nameof(text));

    public async Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        var page = context.GetTerminalPage();
        await page.CopyToClipboardAsync(Text, cancellationToken);
    }

    public override string ToString() => $"Copy \"{Text}\"";
}