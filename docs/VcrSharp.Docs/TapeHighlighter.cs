using Pennington.Highlighting;

namespace VcrSharp.Docs;

/// <summary>
/// Highlights <c>```tape</c> fenced code blocks using the bundled <see cref="TapeTextmateGrammar"/>.
/// <para>
/// Pennington's built-in <see cref="TextMateHighlighter"/> advertises every language via a
/// <c>"*"</c> wildcard at a hard-coded priority of 50, and there is no hook to add a custom
/// grammar to the registry it uses. A second <see cref="TextMateHighlighter"/> would also be a
/// wildcard at priority 50 and never win the tie, so instead this highlighter advertises only
/// <c>tape</c> at a higher priority and delegates the actual tokenization to a
/// <see cref="TextMateHighlighter"/> seeded with the tape grammar.
/// </para>
/// </summary>
internal sealed class TapeHighlighter : ICodeHighlighter
{
    private readonly TextMateHighlighter _inner = new(
        new TextMateLanguageRegistry(registry =>
            registry.AddGrammarFromJson("tape", TapeTextmateGrammar.Grammar)));

    public IReadOnlySet<string> SupportedLanguages { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "tape" };

    // Above the built-in TextMateHighlighter (50) so "tape" routes here; below ShellHighlighter (75).
    public int Priority => 60;

    public string Highlight(string code, string language) => _inner.Highlight(code, language);
}
