namespace VcrSharp.Core.Parsing;

/// <summary>
/// Represents the type of token in a tape file.
/// </summary>
public enum TokenType
{
    // ReSharper disable InconsistentNaming

    // End of file
    EOF,

    // Literals
    STRING,         // "hello", 'world', `text`
    NUMBER,         // 42, 3.14, .5
    DURATION,       // 100ms, 1s, 2m
    JSON,           // { "key": "value" }
    REGEX,          // /pattern/
    BOOLEAN,        // true, false

    // Operators
    AT,             // @
    PLUS,           // +
    SLASH,          // /
    PERCENT,        // %
    EQUALS,         // =

    // Comments
    COMMENT,        // # comment text

    // Whitespace
    NEWLINE,

    // Setting Keywords
    SET,
    OUTPUT,
    REQUIRE,
    SOURCE,

    // Setting Names
    SHELL,
    FONT_FAMILY,
    FONT_SIZE,
    HEIGHT,
    WIDTH,
    LETTER_SPACING,
    LINE_HEIGHT,
    TYPING_SPEED,
    THEME,
    PADDING,
    FRAMERATE,
    PLAYBACK_SPEED,
    LOOP_OFFSET,
    MARGIN_FILL,
    MARGIN,
    WINDOW_BAR_SIZE,
    BORDER_RADIUS,
    CURSOR_BLINK,
    WAIT_TIMEOUT,
    WAIT_PATTERN,

    // Action Keywords
    TYPE,
    SLEEP,
    ENTER,
    SPACE,
    TAB,
    BACKSPACE,
    DELETE,
    INSERT,
    UP,
    DOWN,
    LEFT,
    RIGHT,
    PAGE_UP,
    PAGE_DOWN,
    HOME,
    END,
    ESCAPE,

    // Modifier Keywords
    CTRL,
    ALT,
    SHIFT,

    // Control Keywords
    HIDE,
    SHOW,
    SCREENSHOT,
    WAIT,
    SCREEN,     // For Wait+Screen
    LINE,       // For Wait+Line
    BUFFER,     // For Wait+Buffer

    // Clipboard Keywords
    COPY,
    PASTE,

    // Environment Keywords
    ENV,

    // VcrSharp-specific Keywords
    EXEC,

    // Generic identifier (for unknown keywords or file paths)
    IDENTIFIER,

    // ReSharper restore InconsistentNaming

}

/// <summary>
/// Represents a single token in a tape file.
/// </summary>
public readonly struct Token
{
    public TokenType Type { get; }
    public string Value { get; }
    public int Line { get; }
    public int Column { get; }

    public Token(TokenType type, string value, int line, int column)
    {
        Type = type;
        Value = value;
        Line = line;
        Column = column;
    }

    public override string ToString() => $"{Type}({Value}) at {Line}:{Column}";
}

/// <summary>
/// Extension methods for TokenType.
/// </summary>
public static class TokenTypeExtensions
{
    /// <summary>
    /// Returns true if the token type represents a setting name.
    /// </summary>
    public static bool IsSetting(this TokenType type)
    {
        return type is
            TokenType.SHELL or
            TokenType.FONT_FAMILY or
            TokenType.FONT_SIZE or
            TokenType.HEIGHT or
            TokenType.WIDTH or
            TokenType.LETTER_SPACING or
            TokenType.LINE_HEIGHT or
            TokenType.TYPING_SPEED or
            TokenType.THEME or
            TokenType.PADDING or
            TokenType.FRAMERATE or
            TokenType.PLAYBACK_SPEED or
            TokenType.LOOP_OFFSET or
            TokenType.MARGIN_FILL or
            TokenType.MARGIN or
            TokenType.WINDOW_BAR_SIZE or
            TokenType.BORDER_RADIUS or
            TokenType.CURSOR_BLINK or
            TokenType.WAIT_TIMEOUT or
            TokenType.WAIT_PATTERN;
    }

    /// <summary>
    /// Returns true if the token type represents an action command.
    /// </summary>
    public static bool IsActionCommand(this TokenType type)
    {
        return type is
            TokenType.TYPE or
            TokenType.SLEEP or
            TokenType.ENTER or
            TokenType.SPACE or
            TokenType.TAB or
            TokenType.BACKSPACE or
            TokenType.DELETE or
            TokenType.INSERT or
            TokenType.UP or
            TokenType.DOWN or
            TokenType.LEFT or
            TokenType.RIGHT or
            TokenType.PAGE_UP or
            TokenType.PAGE_DOWN or
            TokenType.HOME or
            TokenType.END or
            TokenType.ESCAPE or
            TokenType.EXEC;
    }

    /// <summary>
    /// Returns true if the token type represents a modifier key.
    /// </summary>
    public static bool IsModifier(this TokenType type)
    {
        return type is TokenType.CTRL or TokenType.ALT or TokenType.SHIFT;
    }

    /// <summary>
    /// Returns true if the token type represents a control command.
    /// </summary>
    public static bool IsControlCommand(this TokenType type)
    {
        return type is
            TokenType.HIDE or
            TokenType.SHOW or
            TokenType.SCREENSHOT or
            TokenType.WAIT or
            TokenType.COPY or
            TokenType.PASTE;
    }
}