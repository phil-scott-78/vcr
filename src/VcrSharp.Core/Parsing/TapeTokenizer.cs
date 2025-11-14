using Superpower;
using Superpower.Model;
using Superpower.Parsers;
using Superpower.Tokenizers;

namespace VcrSharp.Core.Parsing;

/// <summary>
/// Tokenizer for tape files.
/// </summary>
public static class TapeTokenizer
{
    // Escape sequence parser for double-quoted strings - returns string to preserve unknown escapes
    private static TextParser<string> EscapeSequence =>
        from backslash in Character.EqualTo('\\')
        from escaped in Character.AnyChar
        select escaped switch
        {
            'n' => "\n",
            't' => "\t",
            'r' => "\r",
            '\\' => "\\",
            '"' => "\"",
            _ => $"\\{escaped}" // Unknown escape: preserve backslash for backward compatibility
        };

    // Double-quoted string with escape support
    private static TextParser<Unit> DoubleQuotedString =>
        from open in Character.EqualTo('"')
        from parts in EscapeSequence.Or(Character.ExceptIn('"', '\\').Select(c => c.ToString())).Many()
        from close in Character.EqualTo('"')
        select Unit.Value;

    // Single-quoted strings remain completely literal (no escape processing)
    private static TextParser<Unit> SingleQuotedString =>
        from open in Character.EqualTo('\'')
        from content in Character.Except('\'').Many()
        from close in Character.EqualTo('\'')
        select Unit.Value;

    // Backtick-quoted strings remain completely literal (no escape processing)
    private static TextParser<Unit> BacktickQuotedString =>
        from open in Character.EqualTo('`')
        from content in Character.Except('`').Many()
        from close in Character.EqualTo('`')
        select Unit.Value;

    // Number parser (including negative and decimal)
    private static TextParser<Unit> Number =>
        Numerics.Decimal.Value(Unit.Value);

    // Duration parser (number followed by unit)
    // Must match the entire pattern atomically to prevent "2m" from being split into "2" + "m"
    private static readonly TextParser<Unit> Duration =
        // Match optional negative, digits, optional decimal, then required unit
        from negative in Character.EqualTo('-').OptionalOrDefault()
        from intDigits in Character.Digit.AtLeastOnce()
        from dot in Character.EqualTo('.').OptionalOrDefault()
        from fracDigits in Character.Digit.Many()
        from unit in Span.EqualTo("ms").Or(Span.EqualTo("m")).Or(Span.EqualTo("s"))
        select Unit.Value;

    // Regex pattern parser
    private static TextParser<Unit> Regex =>
        from open in Character.EqualTo('/')
        from pattern in Character.Except('/').Many()
        from close in Character.EqualTo('/')
        select Unit.Value;

    // Identifier parser (letter followed by letter/digit/underscore/dot/slash for file paths)
    private static TextParser<Unit> Identifier =>
        from first in Character.Letter
        from rest in Character.LetterOrDigit.Or(Character.In('_', '.', '/', '\\')).Many()
        select Unit.Value;

    /// <summary>
    /// Creates the tokenizer for tape files.
    /// </summary>
    public static Tokenizer<TapeToken> Create()
    {
        return new TokenizerBuilder<TapeToken>()
            // Ignore comments (must come before other patterns)
            .Ignore(Character.EqualTo('#')
                .IgnoreThen(Character.ExceptIn('\n', '\r').Many())
                .Value(Unit.Value))

            // Ignore newlines (they're just command separators)
            .Ignore(Span.EqualTo("\r\n"))
            .Ignore(Character.EqualTo('\n'))
            .Ignore(Character.EqualTo('\r'))

            // Wait scopes (must come before Plus operator and keywords)
            .Match(Span.EqualTo("+Screen"), TapeToken.PlusScreen, requireDelimiters: true)
            .Match(Span.EqualTo("+Buffer"), TapeToken.PlusBuffer, requireDelimiters: true)
            .Match(Span.EqualTo("+Line"), TapeToken.PlusLine, requireDelimiters: true)

            // Keywords - Commands (require delimiters to avoid matching inside identifiers)
            // Note: "Wait+Line" still works because "+" is a delimiter
            .Match(Span.EqualTo("Set"), TapeToken.Set, requireDelimiters: true)
            .Match(Span.EqualTo("Output"), TapeToken.Output, requireDelimiters: true)
            .Match(Span.EqualTo("Require"), TapeToken.Require, requireDelimiters: true)
            .Match(Span.EqualTo("Source"), TapeToken.Source, requireDelimiters: true)
            .Match(Span.EqualTo("Type"), TapeToken.Type, requireDelimiters: true)
            .Match(Span.EqualTo("Sleep"), TapeToken.Sleep, requireDelimiters: true)
            .Match(Span.EqualTo("Wait"), TapeToken.Wait)
            .Match(Span.EqualTo("Hide"), TapeToken.Hide, requireDelimiters: true)
            .Match(Span.EqualTo("Show"), TapeToken.Show, requireDelimiters: true)
            .Match(Span.EqualTo("Screenshot"), TapeToken.Screenshot, requireDelimiters: true)
            .Match(Span.EqualTo("Copy"), TapeToken.Copy, requireDelimiters: true)
            .Match(Span.EqualTo("Paste"), TapeToken.Paste, requireDelimiters: true)
            .Match(Span.EqualTo("Env"), TapeToken.Env, requireDelimiters: true)
            .Match(Span.EqualTo("Exec"), TapeToken.Exec, requireDelimiters: true)

            // Keywords - Special keys (require delimiters to avoid matching inside identifiers like "EndBuffer")
            .Match(Span.EqualTo("Enter"), TapeToken.Enter, requireDelimiters: true)
            .Match(Span.EqualTo("Space"), TapeToken.Space, requireDelimiters: true)
            .Match(Span.EqualTo("Tab"), TapeToken.Tab, requireDelimiters: true)
            .Match(Span.EqualTo("Backspace"), TapeToken.Backspace, requireDelimiters: true)
            .Match(Span.EqualTo("Delete"), TapeToken.Delete, requireDelimiters: true)
            .Match(Span.EqualTo("Insert"), TapeToken.Insert, requireDelimiters: true)
            .Match(Span.EqualTo("Escape"), TapeToken.Escape, requireDelimiters: true)
            .Match(Span.EqualTo("PageUp"), TapeToken.PageUp, requireDelimiters: true)
            .Match(Span.EqualTo("PageDown"), TapeToken.PageDown, requireDelimiters: true)
            .Match(Span.EqualTo("Home"), TapeToken.Home, requireDelimiters: true)
            .Match(Span.EqualTo("End"), TapeToken.End, requireDelimiters: true)
            .Match(Span.EqualTo("Up"), TapeToken.Up, requireDelimiters: true)
            .Match(Span.EqualTo("Down"), TapeToken.Down, requireDelimiters: true)
            .Match(Span.EqualTo("Left"), TapeToken.Left, requireDelimiters: true)
            .Match(Span.EqualTo("Right"), TapeToken.Right, requireDelimiters: true)

            // Keywords - Modifiers (no delimiters so "Ctrl+C" works)
            .Match(Span.EqualTo("Ctrl"), TapeToken.Ctrl)
            .Match(Span.EqualTo("Alt"), TapeToken.Alt)
            .Match(Span.EqualTo("Shift"), TapeToken.Shift)

            // Keywords - Boolean (case-insensitive, no delimiters)
            .Match(Span.EqualToIgnoreCase("true"), TapeToken.True)
            .Match(Span.EqualToIgnoreCase("false"), TapeToken.False)

            // Operators
            .Match(Character.EqualTo('@'), TapeToken.At)
            .Match(Character.EqualTo('+'), TapeToken.Plus)

            // String literals (order matters - most specific first)
            .Match(DoubleQuotedString, TapeToken.String)
            .Match(SingleQuotedString, TapeToken.StringLiteral)
            .Match(BacktickQuotedString, TapeToken.StringLiteral)

            // Regex pattern
            .Match(Regex, TapeToken.Regex)

            // Duration (must come before Number to match "500ms" as Duration not Number+"ms")
            .Match(Duration, TapeToken.Duration)

            // Number
            .Match(Number, TapeToken.Number)

            // Identifier (after keywords to avoid matching keywords as identifiers)
            .Match(Identifier, TapeToken.Identifier)

            // Ignore whitespace (spaces and tabs)
            .Ignore(Span.WhiteSpace)

            .Build();
    }
}
