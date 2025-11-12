using Superpower.Display;

namespace VcrSharp.Core.Parsing;

/// <summary>
/// Token types for tape file parsing.
/// </summary>
public enum TapeToken
{
    // Keywords - Commands
    [Token(Category = "keyword", Example = "Set")]
    Set,

    [Token(Category = "keyword", Example = "Output")]
    Output,

    [Token(Category = "keyword", Example = "Require")]
    Require,

    [Token(Category = "keyword", Example = "Source")]
    Source,

    [Token(Category = "keyword", Example = "Type")]
    Type,

    [Token(Category = "keyword", Example = "Sleep")]
    Sleep,

    [Token(Category = "keyword", Example = "Wait")]
    Wait,

    [Token(Category = "keyword", Example = "Hide")]
    Hide,

    [Token(Category = "keyword", Example = "Show")]
    Show,

    [Token(Category = "keyword", Example = "Screenshot")]
    Screenshot,

    [Token(Category = "keyword", Example = "Copy")]
    Copy,

    [Token(Category = "keyword", Example = "Paste")]
    Paste,

    [Token(Category = "keyword", Example = "Env")]
    Env,

    [Token(Category = "keyword", Example = "Exec")]
    Exec,

    // Keywords - Special keys
    [Token(Category = "keyword", Example = "Enter")]
    Enter,

    [Token(Category = "keyword", Example = "Space")]
    Space,

    [Token(Category = "keyword", Example = "Tab")]
    Tab,

    [Token(Category = "keyword", Example = "Backspace")]
    Backspace,

    [Token(Category = "keyword", Example = "Delete")]
    Delete,

    [Token(Category = "keyword", Example = "Insert")]
    Insert,

    [Token(Category = "keyword", Example = "Escape")]
    Escape,

    [Token(Category = "keyword", Example = "Up")]
    Up,

    [Token(Category = "keyword", Example = "Down")]
    Down,

    [Token(Category = "keyword", Example = "Left")]
    Left,

    [Token(Category = "keyword", Example = "Right")]
    Right,

    [Token(Category = "keyword", Example = "PageUp")]
    PageUp,

    [Token(Category = "keyword", Example = "PageDown")]
    PageDown,

    [Token(Category = "keyword", Example = "Home")]
    Home,

    [Token(Category = "keyword", Example = "End")]
    End,

    // Keywords - Modifiers
    [Token(Category = "keyword", Example = "Ctrl")]
    Ctrl,

    [Token(Category = "keyword", Example = "Alt")]
    Alt,

    [Token(Category = "keyword", Example = "Shift")]
    Shift,

    // Keywords - Wait scopes
    [Token(Category = "operator", Example = "+Screen")]
    PlusScreen,

    [Token(Category = "operator", Example = "+Buffer")]
    PlusBuffer,

    [Token(Category = "operator", Example = "+Line")]
    PlusLine,

    // Keywords - Boolean
    [Token(Category = "literal", Example = "true")]
    True,

    [Token(Category = "literal", Example = "false")]
    False,

    // Operators
    [Token(Category = "operator", Example = "@")]
    At,

    [Token(Category = "operator", Example = "+")]
    Plus,

    // Literals
    [Token(Category = "string", Example = "\"hello world\"")]
    String,

    [Token(Category = "string", Example = "'literal string'")]
    StringLiteral,

    [Token(Category = "number", Example = "123.45")]
    Number,

    [Token(Category = "duration", Example = "500ms")]
    Duration,

    [Token(Category = "pattern", Example = "/regex/")]
    Regex,

    [Token(Category = "identifier", Description = "identifier")]
    Identifier,

    [Token(Category = "path", Example = "output.gif")]
    FilePath,

    [Token(Category = "character", Example = "C")]
    Character,

    // Structural
    [Token(Category = "comment", Example = "# comment")]
    Comment,

    [Token(Category = "whitespace", Example = "\n")]
    NewLine,
}
