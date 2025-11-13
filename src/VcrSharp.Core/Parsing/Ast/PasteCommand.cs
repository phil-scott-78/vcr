namespace VcrSharp.Core.Parsing.Ast;

/// <summary>
/// Represents a Paste command that pastes clipboard content.
/// </summary>
public class PasteCommand : ICommand
{
    public async Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        var page = context.Page;
        await page.PasteFromClipboardAsync(cancellationToken);
    }

    public override string ToString() => "Paste";
}