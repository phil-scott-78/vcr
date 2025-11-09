namespace VcrSharp.Core.Parsing;

/// <summary>
/// Exception thrown when lexer encounters an error.
/// </summary>
public class LexerException : Exception
{
    public int Line { get; }
    public int Column { get; }
    public string? FilePath { get; }

    public LexerException(string message, int line = 0, int column = 0, string? filePath = null) : base(message)
    {
        Line = line;
        Column = column;
        FilePath = filePath;
    }
}