using System.Globalization;
using System.Text.RegularExpressions;
using Sprache;
using VcrSharp.Core.Parsing.Ast;
// ReSharper disable InconsistentNaming

namespace VcrSharp.Core.Parsing;

/// <summary>
/// Parser for tape files using Sprache parser combinators.
/// </summary>
public class TapeParser
{
    // Basic parsers
    private static readonly Parser<char> SpaceChar = Parse.Char(' ').Or(Parse.Char('\t'));
    private static readonly Parser<string> Spaces = SpaceChar.Many().Text();
    private static readonly Parser<string> Spaces1 = SpaceChar.AtLeastOnce().Text();

    // String parsers with escape handling
    // Escape sequence parser for double-quoted strings - returns string to preserve unknown escapes
    private static readonly Parser<string> EscapeSequence =
        from backslash in Parse.Char('\\')
        from escaped in Parse.AnyChar
        select escaped switch
        {
            'n' => "\n",     // Newline
            't' => "\t",     // Tab
            'r' => "\r",     // Carriage return
            '\\' => "\\",    // Backslash
            '"' => "\"",     // Double quote
            _ => $"\\{escaped}"  // Unknown escape: preserve backslash for backward compatibility
        };

    // Regular character in double-quoted string (not quote, not backslash)
    private static readonly Parser<char> DoubleQuotedRegularChar =
        Parse.AnyChar.Except(Parse.Chars('"', '\\'));

    // Content parser for double-quoted strings (with escape support)
    private static readonly Parser<string> DoubleQuotedStringContent =
        EscapeSequence.Or(DoubleQuotedRegularChar.Select(c => c.ToString()));

    private static readonly Parser<string> DoubleQuotedString =
        from open in Parse.Char('"')
        from parts in DoubleQuotedStringContent.Many()
        from close in Parse.Char('"')
        select string.Concat(parts);

    // Single-quoted strings remain completely literal (no escape processing)
    private static readonly Parser<string> SingleQuotedString =
        from open in Parse.Char('\'')
        from content in Parse.CharExcept('\'').Many().Text()
        from close in Parse.Char('\'')
        select content;

    // Backtick-quoted strings remain completely literal (no escape processing)
    private static readonly Parser<string> BacktickQuotedString =
        from open in Parse.Char('`')
        from content in Parse.CharExcept('`').Many().Text()
        from close in Parse.Char('`')
        select content;

    private static readonly Parser<string> QuotedString =
        DoubleQuotedString.Or(SingleQuotedString).Or(BacktickQuotedString);

    // Number parsers
    private static readonly Parser<double> DecimalNumber =
        from negative in Parse.Char('-').Optional()
        from intPart in Parse.Digit.AtLeastOnce().Text().Or(Parse.Return(""))
        from dot in Parse.Char('.').Optional()
        from fracPart in Parse.Digit.Many().Text()
        select double.Parse(
            (negative.IsDefined ? "-" : "") +
            (string.IsNullOrEmpty(intPart) ? "0" : intPart) +
            (dot.IsDefined ? "." + fracPart : ""),
            CultureInfo.InvariantCulture);

    // Duration parsers
    private static readonly Parser<TimeSpan> Duration =
        from value in DecimalNumber
        from unit in Parse.String("ms").Return("ms")
            .Or(Parse.String("m").Return("m"))
            .Or(Parse.String("s").Return("s"))
        select unit switch
        {
            "ms" => TimeSpan.FromMilliseconds(value),
            "m" => TimeSpan.FromMinutes(value),
            _ => TimeSpan.FromSeconds(value)
        };

    private static readonly Parser<TimeSpan> DurationOrBareNumber =
        Duration.Or(DecimalNumber.Select(d => TimeSpan.FromSeconds(d)));

    // Regex parser
    private static readonly Parser<Regex> RegexPattern =
        from open in Parse.Char('/')
        from pattern in Parse.CharExcept('/').Many().Text()
        from close in Parse.Char('/')
        select new Regex(pattern, RegexOptions.Compiled);

    // Boolean parser
    private static readonly Parser<bool> Boolean =
        Parse.String("true").Return(true)
        .Or(Parse.String("false").Return(false))
        .Or(Parse.String("True").Return(true))
        .Or(Parse.String("False").Return(false));

    // Identifier parser
    private static readonly Parser<string> Identifier =
        from first in Parse.Letter
        from rest in Parse.LetterOrDigit.Or(Parse.Char('_')).Many().Text()
        select first + rest;

    // File path parser (allows dots, slashes, etc.)
    private static readonly Parser<string> FilePath =
        Parse.CharExcept(c => char.IsWhiteSpace(c) || c == '\n' || c == '\r' || c == '#', "file path character")
            .AtLeastOnce().Text();

    // Speed modifier parser (@100ms, @.5, @1s)
    private static readonly Parser<TimeSpan> SpeedModifier =
        from at in Parse.Char('@')
        from duration in DurationOrBareNumber
        select duration;

    // Repeat count parser
    private static readonly Parser<int> RepeatCount =
        from spaces in Spaces1
        from count in Parse.Digit.AtLeastOnce().Text()
        select int.Parse(count);

    // Command parsers

    // Set command
    private static readonly Parser<ICommand> SetCommand =
        from keyword in Parse.String("Set")
        from spaces1 in Spaces1
        from settingName in Identifier
        from spaces2 in Spaces1
        from value in QuotedString
            .Or(Duration.Select(d => d.ToString()))
            .Or(DecimalNumber.Select(n => n.ToString(CultureInfo.InvariantCulture)))
            .Or(Boolean.Select(b => b.ToString()))
        select new SetCommand(settingName, value);

    // Output command
    private static readonly Parser<ICommand> OutputCommand =
        from keyword in Parse.String("Output")
        from spaces in Spaces1
        from path in QuotedString.Or(FilePath)
        select new OutputCommand(path);

    // Require command
    private static readonly Parser<ICommand> RequireCommand =
        from keyword in Parse.String("Require")
        from spaces in Spaces1
        from program in Identifier
        select new RequireCommand(program);

    // Source command
    private static readonly Parser<ICommand> SourceCommand =
        from keyword in Parse.String("Source")
        from spaces in Spaces1
        from path in QuotedString.Or(FilePath)
        select new SourceCommand(path);

    // Type command
    private static readonly Parser<ICommand> TypeCommand =
        from keyword in Parse.String("Type")
        from speed in SpeedModifier.Optional()
        from spaces in Spaces1
        from text in QuotedString
        select new TypeCommand(text, speed.IsDefined ? speed.Get() : null);

    // Sleep command
    private static readonly Parser<ICommand> SleepCommand =
        from keyword in Parse.String("Sleep")
        from spaces in Spaces1
        from duration in DurationOrBareNumber
        select new SleepCommand(duration);

    // Key command (Enter, Tab, arrows, etc.)
    private static Parser<ICommand> KeyCommandFor(string keyName) =>
        from keyword in Parse.String(keyName)
        from speed in SpeedModifier.Optional()
        from count in RepeatCount.Optional()
        select new KeyCommand(keyName, count.IsDefined ? count.Get() : 1, speed.IsDefined ? speed.Get() : null);

    private static readonly Parser<ICommand> KeyCommand =
        KeyCommandFor("Enter")
        .Or(KeyCommandFor("Space"))
        .Or(KeyCommandFor("Tab"))
        .Or(KeyCommandFor("Backspace"))
        .Or(KeyCommandFor("Delete"))
        .Or(KeyCommandFor("Insert"))
        .Or(KeyCommandFor("Escape"))
        .Or(KeyCommandFor("Up"))
        .Or(KeyCommandFor("Down"))
        .Or(KeyCommandFor("Left"))
        .Or(KeyCommandFor("Right"))
        .Or(KeyCommandFor("PageUp"))
        .Or(KeyCommandFor("PageDown"))
        .Or(KeyCommandFor("Home"))
        .Or(KeyCommandFor("End"));

    // Modifier command (Ctrl+C, Alt+Enter, etc.)
    private static readonly Parser<string> ModifierKey =
        Parse.String("Enter").Text()
        .Or(Parse.String("Tab").Text())
        .Or(Parse.String("Space").Text())
        .Or(Parse.String("Escape").Text())
        .Or(Parse.String("Backspace").Text())
        .Or(Parse.String("Delete").Text())
        .Or(Parse.AnyChar.Select(c => c.ToString()));

    private static readonly Parser<ICommand> ModifierCommand =
        from ctrl in Parse.String("Ctrl").Optional()
        from plus1 in Parse.Char('+').Optional()
        from shift in Parse.String("Shift").Optional()
        from plus2 in Parse.Char('+').Optional()
        from alt in Parse.String("Alt").Optional()
        from plus3 in Parse.Char('+').Optional()
        from key in ModifierKey
        where ctrl.IsDefined || alt.IsDefined || shift.IsDefined
        select new ModifierCommand(
            ctrl.IsDefined,
            alt.IsDefined,
            shift.IsDefined,
            key);

    // Wait command (complex: Wait[+Scope][@timeout] [/pattern/])
    private static readonly Parser<ICommand> WaitCommand =
        from keyword in Parse.String("Wait")
        from scope in Parse.String("+Screen").Return(WaitScope.Screen)
            .Or(Parse.String("+Buffer").Return(WaitScope.Buffer))
            .Or(Parse.String("+Line").Return(WaitScope.Line))
            .Optional()
        from timeout in SpeedModifier.Optional()
        from spaces in Spaces.Optional()
        from pattern in RegexPattern.Optional()
        select new WaitCommand(
            scope.IsDefined ? scope.Get() : WaitScope.Buffer,
            timeout.IsDefined ? timeout.Get() : null,
            pattern.IsDefined ? pattern.Get() : null);

    // Hide command
    private static readonly Parser<ICommand> HideCommand =
        from keyword in Parse.String("Hide")
        select new HideCommand();

    // Show command
    private static readonly Parser<ICommand> ShowCommand =
        from keyword in Parse.String("Show")
        select new ShowCommand();

    // Screenshot command
    private static readonly Parser<ICommand> ScreenshotCommand =
        from keyword in Parse.String("Screenshot")
        from spaces in Spaces1
        from path in QuotedString.Or(FilePath)
        select new ScreenshotCommand(path);

    // Copy command
    private static readonly Parser<ICommand> CopyCommand =
        from keyword in Parse.String("Copy")
        from spaces in Spaces1
        from text in QuotedString
        select new CopyCommand(text);

    // Paste command
    private static readonly Parser<ICommand> PasteCommand =
        from keyword in Parse.String("Paste")
        select new PasteCommand();

    // Env command
    private static readonly Parser<ICommand> EnvCommand =
        from keyword in Parse.String("Env")
        from spaces1 in Spaces1
        from key in Identifier
        from spaces2 in Spaces1
        from value in QuotedString
        select new EnvCommand(key, value);

    // Exec command
    private static readonly Parser<ICommand> ExecCommand =
        from keyword in Parse.String("Exec")
        from spaces in Spaces1
        from command in QuotedString
        select new ExecCommand(command);

    // Combined command parser - order matters!
    private static readonly Parser<ICommand> Command =
        SetCommand
        .Or(OutputCommand)
        .Or(RequireCommand)
        .Or(SourceCommand)
        .Or(TypeCommand)
        .Or(SleepCommand)
        .Or(WaitCommand)
        .Or(HideCommand)
        .Or(ShowCommand)
        .Or(ScreenshotCommand)
        .Or(CopyCommand)
        .Or(PasteCommand)
        .Or(EnvCommand)
        .Or(ExecCommand)
        .Or(ModifierCommand)
        .Or(KeyCommand);

    // Line parser (handles inline commands - multiple commands per line)
    private static readonly Parser<IEnumerable<ICommand>> CommandLine =
        Command.DelimitedBy(Spaces1);

    // Comment parser
    private static readonly Parser<string> Comment =
        from hash in Parse.Char('#')
        from content in Parse.AnyChar.Except(Parse.Char('\n').Or(Parse.Char('\r'))).Many().Text()
        select content;

    // Empty line or comment line
    private static readonly Parser<IEnumerable<ICommand>> EmptyOrCommentLine =
        from spaces in Spaces
        from comment in Comment.Optional()
        select Enumerable.Empty<ICommand>();

    // Full tape file parser
    private static readonly Parser<IEnumerable<ICommand>> TapeFile =
        from lines in EmptyOrCommentLine.Or(from spaces in Spaces
                                            from commands in CommandLine
                                            from trailing in Spaces
                                            select commands)
            .DelimitedBy(Parse.LineEnd)
        from trailingLines in Parse.LineEnd.Many()
        select lines.SelectMany(x => x);

    /// <summary>
    /// Validates SET command usage rules:
    /// 1. SET commands can only appear once per setting name
    /// 2. SET commands must appear before action commands
    /// </summary>
    private static void ValidateSetCommands(List<ICommand> commands, string? filePath = null)
    {
        var seenSettings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var actionCommandEncountered = false;
        var lineNumber = 1;

        foreach (var command in commands)
        {
            // Check if this is an action command
            var isActionCommand = command is Ast.TypeCommand or Ast.KeyCommand or Ast.ModifierCommand
                or Ast.SleepCommand or Ast.WaitCommand or Ast.HideCommand or Ast.ShowCommand
                or Ast.ScreenshotCommand or Ast.CopyCommand or Ast.PasteCommand or Ast.ExecCommand;

            if (isActionCommand)
            {
                actionCommandEncountered = true;
            }

            // Validate SET commands
            if (command is SetCommand setCommand)
            {
                // Check for duplicate SET commands
                if (seenSettings.Contains(setCommand.SettingName))
                {
                    throw new TapeParseException(
                        $"Duplicate SET command: '{setCommand.SettingName}' has already been set",
                        null,
                        lineNumber,
                        1,
                        filePath);
                }

                // Check if SET appears after action commands
                if (actionCommandEncountered)
                {
                    throw new TapeParseException(
                        $"SET command for '{setCommand.SettingName}' appears after action commands. All SET commands must appear before Type, Exec, Sleep, Wait, and other action commands",
                        null,
                        lineNumber,
                        1,
                        filePath);
                }

                seenSettings.Add(setCommand.SettingName);
            }

            lineNumber++;
        }
    }

    /// <summary>
    /// Parses a tape file and returns a list of commands.
    /// </summary>
    public List<ICommand> ParseTape(string source, string? filePath = null)
    {
        try
        {
            var result = TapeFile.End().Parse(source);
            var commandList = result.ToList();

            // Validate SET command rules
            ValidateSetCommands(commandList, filePath);

            return commandList;
        }
        catch (ParseException ex)
        {
            var messages = ex.Message.Split(';');
            throw new TapeParseException(
                messages[0],
                ex,
                ex.Position.Line,
                ex.Position.Column,
                filePath);
        }
    }

    /// <summary>
    /// Parses a tape file from a file path.
    /// </summary>
    public async Task<List<ICommand>> ParseFileAsync(string filePath)
    {
        var source = await File.ReadAllTextAsync(filePath);
        return ParseTape(source, filePath);
    }
}

/// <summary>
/// Exception thrown when tape parsing fails.
/// </summary>
public class TapeParseException : Exception
{
    public int Line { get; }
    public int Column { get; }
    public string? FilePath { get; }

    public TapeParseException(string message, int line = 0, int column = 0, string? filePath = null) : base(message)
    {
        Line = line;
        Column = column;
        FilePath = filePath;
    }

    public TapeParseException(string message, Exception? innerException, int line = 0, int column = 0, string? filePath = null) : base(message, innerException)
    {
        Line = line;
        Column = column;
        FilePath = filePath;
    }
}