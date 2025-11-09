namespace VcrSharp.Core.Parsing.Ast;

/// <summary>
/// Represents a modifier key combination command (Ctrl+C, Alt+Enter, etc.).
/// Example: Ctrl+C, Ctrl+Alt+Delete, Shift+Tab
/// </summary>
public class ModifierCommand(bool hasCtrl, bool hasAlt, bool hasShift, string key)
    : ICommand
{
    public bool HasCtrl { get; } = hasCtrl;
    public bool HasAlt { get; } = hasAlt;
    public bool HasShift { get; } = hasShift;
    public string Key { get; } = key;

    public async Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        var page = context.GetTerminalPage();

        // Build modifiers list
        var modifiers = new List<string>();
        if (HasCtrl)
            modifiers.Add(Helpers.KeyMapper.MapModifier("Ctrl"));
        if (HasAlt)
            modifiers.Add(Helpers.KeyMapper.MapModifier("Alt"));
        if (HasShift)
            modifiers.Add(Helpers.KeyMapper.MapModifier("Shift"));

        // Map main key
        var mappedKey = Helpers.KeyMapper.MapKey(Key);

        // Press key combination
        await page.PressKeyCombinationAsync(modifiers, mappedKey, cancellationToken);
    }

    public override string ToString()
    {
        var parts = new List<string>();
        if (HasCtrl) parts.Add("Ctrl");
        if (HasAlt) parts.Add("Alt");
        if (HasShift) parts.Add("Shift");
        parts.Add(Key);
        return string.Join("+", parts);
    }
}