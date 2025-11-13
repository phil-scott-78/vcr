namespace VcrSharp.Core.Parsing.Ast;

/// <summary>
/// Represents a Copy command that copies text to clipboard.
/// Example: Copy "hello world"
/// </summary>
public class CopyCommand : ICommand
{
    public string Text { get; }

    public CopyCommand(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
    }

    public async Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        var page = context.Page;
        await page.CopyToClipboardAsync(Text, cancellationToken);
    }

    public override string ToString() => $"Copy \"{Text}\"";
}