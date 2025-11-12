using Errata;
using Spectre.Console;
using VcrSharp.Core.Parsing;

namespace VcrSharp.Cli.Helpers;

/// <summary>
/// Helper class for displaying parse errors using Spectre.Errata.
/// </summary>
public static class ErrorReporter
{
    /// <summary>
    /// Displays a parse error with rich formatting and source code context.
    /// </summary>
    public static void DisplayParseError(Exception exception, string filePath)
    {
        if (exception is TapeParseException { Line: > 0 } tapeEx)
        {
            DisplayTapeParseError(tapeEx, filePath);
        }
        else
        {
            // Fallback for non-parse exceptions
            AnsiConsole.MarkupLineInterpolated($"[bold red]Error:[/] {exception.Message}");
            if (exception.InnerException != null)
            {
                AnsiConsole.MarkupLineInterpolated($"[dim]{exception.InnerException.Message}[/]");
            }
        }
    }

    private static void DisplayTapeParseError(TapeParseException exception, string filePath)
    {
        try
        {
            // Read the source file
            var sourceText = File.ReadAllText(filePath);
            var fileName = Path.GetFileName(filePath);

            // Create in-memory repository with the source file
            var repository = new InMemorySourceRepository();
            repository.Register(fileName, sourceText);

            var report = new Report(repository);

            // Calculate error length to highlight the problematic token/word
            var errorLength = CalculateErrorLength(sourceText, exception.Line, exception.Column);

            var diagnostic = Diagnostic.Error("Parsing failure")
                .WithCode("TAPE_PARSE")
                .WithLabel(new Label(
                    fileName,
                    new Location(exception.Line, exception.Column),
                    exception.Message)
                    .WithLength(errorLength)
                    .WithContextLines(3)
                    .WithColor(Color.Red));

            report.AddDiagnostic(diagnostic);
            report.Render(AnsiConsole.Console);
        }
        catch
        {
            // Fallback if Errata fails
            AnsiConsole.MarkupLineInterpolated($"[bold red]Parse Error at line {exception.Line}, column {exception.Column}:[/]");
            AnsiConsole.MarkupLineInterpolated($"[red]{exception.Message}[/]");
        }
    }

    /// <summary>
    /// Calculates the length of text to highlight for an error.
    /// Highlights from the error position to the end of the current word/token or end of line.
    /// </summary>
    private static int CalculateErrorLength(string sourceText, int line, int column)
    {
        try
        {
            var lines = sourceText.Split('\n');
            if (line < 1 || line > lines.Length)
                return 1;

            var errorLine = lines[line - 1]; // Lines are 1-indexed
            var startIndex = column - 1; // Columns are 1-indexed

            if (startIndex < 0 || startIndex >= errorLine.Length)
                return 1;

            // Find the end of the current token (word, string, or to end of line)
            var endIndex = startIndex;

            // If we're at a quote, highlight to the end of line (for unterminated strings)
            if (startIndex < errorLine.Length && (errorLine[startIndex] == '"' || errorLine[startIndex] == '\'' || errorLine[startIndex] == '`'))
            {
                endIndex = errorLine.Length - 1;
            }
            else
            {
                // Otherwise, highlight the current word/token
                while (endIndex < errorLine.Length && !char.IsWhiteSpace(errorLine[endIndex]))
                {
                    endIndex++;
                }
            }

            var length = endIndex - startIndex;
            return length > 0 ? length : 1; // At least 1 character
        }
        catch
        {
            return 1; // Fallback to single character
        }
    }
}