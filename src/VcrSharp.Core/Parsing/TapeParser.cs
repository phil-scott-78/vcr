using System.Globalization;
using System.Text.RegularExpressions;
using Superpower;
using Superpower.Parsers;
using VcrSharp.Core.Parsing.Ast;
// ReSharper disable InconsistentNaming

namespace VcrSharp.Core.Parsing;

/// <summary>
/// Parser for tape files using Superpower parser combinators.
/// </summary>
public class TapeParser
{
    private static readonly Tokenizer<TapeToken> Tokenizer = TapeTokenizer.Create();

    // Helper to strip quotes from string tokens
    private static string StripQuotes(string value)
    {
        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') ||
             (value[0] == '\'' && value[^1] == '\'') ||
             (value[0] == '`' && value[^1] == '`')))
        {
            return value.Substring(1, value.Length - 2);
        }
        return value;
    }

    // Helper to process escape sequences in double-quoted strings
    private static string ProcessEscapes(string value)
    {
        // Only process escapes for double-quoted strings
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            var content = value.Substring(1, value.Length - 2);
            // Process escape sequences
            var result = new System.Text.StringBuilder();
            for (var i = 0; i < content.Length; i++)
            {
                if (content[i] == '\\' && i + 1 < content.Length)
                {
                    var next = content[i + 1];
                    switch (next)
                    {
                        case 'n':
                            result.Append('\n');
                            i++;
                            break;
                        case 't':
                            result.Append('\t');
                            i++;
                            break;
                        case 'r':
                            result.Append('\r');
                            i++;
                            break;
                        case '\\':
                            result.Append('\\');
                            i++;
                            break;
                        case '"':
                            result.Append('"');
                            i++;
                            break;
                        default:
                            // Unknown escape: preserve backslash for backward compatibility
                            result.Append('\\');
                            result.Append(next);
                            i++;
                            break;
                    }
                }
                else
                {
                    result.Append(content[i]);
                }
            }
            return result.ToString();
        }
        return StripQuotes(value);
    }

    // Helper parsers for common patterns

    private static readonly TokenListParser<TapeToken, string> QuotedString =
        Token.EqualTo(TapeToken.String).Select(t => ProcessEscapes(t.ToStringValue()))
        .Or(Token.EqualTo(TapeToken.StringLiteral).Select(t => StripQuotes(t.ToStringValue())));

    private static readonly TokenListParser<TapeToken, string> Identifier =
        Token.EqualTo(TapeToken.Identifier).Select(t => t.ToStringValue());

    private static readonly TokenListParser<TapeToken, string> FilePath =
        QuotedString.Or(Identifier);

    private static readonly TokenListParser<TapeToken, double> Number =
        Token.EqualTo(TapeToken.Number).Apply(Numerics.DecimalDouble);

    private static readonly TokenListParser<TapeToken, bool> Boolean =
        Token.EqualTo(TapeToken.True).Value(true)
        .Or(Token.EqualTo(TapeToken.False).Value(false));

    private static readonly TokenListParser<TapeToken, Regex?> RegexPattern =
        Token.EqualTo(TapeToken.Regex).Select(t =>
        {
            var pattern = t.ToStringValue();
            // Remove the surrounding slashes
            if (pattern.StartsWith('/') && pattern.EndsWith('/') && pattern.Length >= 2)
                pattern = pattern.Substring(1, pattern.Length - 2);
            return (Regex?)new Regex(pattern, RegexOptions.Compiled);
        });

    // Duration parser - handles both Duration tokens (500ms) and bare numbers (interpreted as seconds)
    private static readonly TokenListParser<TapeToken, TimeSpan> Duration =
        Token.EqualTo(TapeToken.Duration).Select(t =>
        {
            var value = t.ToStringValue();
            // Parse the duration string (e.g., "500ms", "2s", "1.5m")
            if (value.EndsWith("ms"))
            {
                var num = double.Parse(value[..^2], CultureInfo.InvariantCulture);
                return TimeSpan.FromMilliseconds(num);
            }
            else if (value.EndsWith("m") && !value.EndsWith("ms"))
            {
                var num = double.Parse(value[..^1], CultureInfo.InvariantCulture);
                return TimeSpan.FromMinutes(num);
            }
            else if (value.EndsWith("s"))
            {
                var num = double.Parse(value[..^1], CultureInfo.InvariantCulture);
                return TimeSpan.FromSeconds(num);
            }
            throw new InvalidOperationException($"Invalid duration format: {value}");
        });

    // Parser for duration that handles both Duration tokens and Number tokens (with optional unit)
    private static readonly TokenListParser<TapeToken, TimeSpan> DurationOrBareNumber =
        Duration.Or(
            from num in Number
            from unit in Identifier!.OptionalOrDefault()
            select unit != null
                ? unit.ToLower() switch
                {
                    "ms" => TimeSpan.FromMilliseconds(num),
                    "m" => TimeSpan.FromMinutes(num),
                    "s" => TimeSpan.FromSeconds(num),
                    _ => throw new InvalidOperationException($"Invalid duration unit: {unit}")
                }
                : TimeSpan.FromSeconds(num)  // Bare number defaults to seconds
        );

    // Speed modifier parser (@100ms, @.5, @1s)
    private static readonly TokenListParser<TapeToken, TimeSpan> SpeedModifier =
        from at in Token.EqualTo(TapeToken.At)
        from duration in DurationOrBareNumber
        select duration;

    // Repeat count parser (space-separated number for key commands)
    private static readonly TokenListParser<TapeToken, int> RepeatCount =
        Number.Select(n => (int)n);

    // Command parsers

    // Set command: Set FontSize 32
    private static readonly TokenListParser<TapeToken, ICommand> SetCommand =
        from keyword in Token.EqualTo(TapeToken.Set)
        from settingName in Identifier
        from value in QuotedString
            .Or(Duration.Select(d => d.ToString()))
            .Or(Number.Select(n => n.ToString(CultureInfo.InvariantCulture)))
            .Or(Boolean.Select(b => b.ToString()))
        select (ICommand)new SetCommand(settingName, value, keyword.Position.Line);

    // Output command: Output demo.gif
    private static readonly TokenListParser<TapeToken, ICommand> OutputCommand =
        from keyword in Token.EqualTo(TapeToken.Output)
        from path in FilePath
        select (ICommand)new OutputCommand(path);

    // Require command: Require npm
    private static readonly TokenListParser<TapeToken, ICommand> RequireCommand =
        from keyword in Token.EqualTo(TapeToken.Require)
        from program in Identifier
        select (ICommand)new RequireCommand(program);

    // Source command: Source other.tape
    private static readonly TokenListParser<TapeToken, ICommand> SourceCommand =
        from keyword in Token.EqualTo(TapeToken.Source)
        from path in FilePath
        select (ICommand)new SourceCommand(path);

    // Type command: Type "hello" or Type@500ms "hello"
    private static readonly TokenListParser<TapeToken, ICommand> TypeCommand =
        from keyword in Token.EqualTo(TapeToken.Type)
        from speed in SpeedModifier.OptionalOrDefault()
        from text in QuotedString
        select (ICommand)new TypeCommand(text, speed == TimeSpan.Zero ? null : speed);

    // Sleep command: Sleep 1s
    private static readonly TokenListParser<TapeToken, ICommand> SleepCommand =
        from keyword in Token.EqualTo(TapeToken.Sleep)
        from duration in DurationOrBareNumber
        select (ICommand)new SleepCommand(duration);

    // Key command helper for specific keys
    // Also accepts identifiers matching key names to handle tokenizer ambiguity (e.g., "End" vs "EndBuffer")
    private static TokenListParser<TapeToken, ICommand> KeyCommandFor(TapeToken keyToken, string keyName) =>
        from keyword in Token.EqualTo(keyToken).Or(Token.EqualTo(TapeToken.Identifier).Where(t => t.ToStringValue().Equals(keyName, StringComparison.OrdinalIgnoreCase)))
        from speed in SpeedModifier.OptionalOrDefault()
        from count in RepeatCount.OptionalOrDefault()
        select (ICommand)new KeyCommand(keyName, count == 0 ? 1 : count, speed == TimeSpan.Zero ? null : speed);

    // Key command: Enter, Tab, etc.
    private static readonly TokenListParser<TapeToken, ICommand> KeyCommand =
        KeyCommandFor(TapeToken.Enter, "Enter")
        .Or(KeyCommandFor(TapeToken.Space, "Space"))
        .Or(KeyCommandFor(TapeToken.Tab, "Tab"))
        .Or(KeyCommandFor(TapeToken.Backspace, "Backspace"))
        .Or(KeyCommandFor(TapeToken.Delete, "Delete"))
        .Or(KeyCommandFor(TapeToken.Insert, "Insert"))
        .Or(KeyCommandFor(TapeToken.Escape, "Escape"))
        .Or(KeyCommandFor(TapeToken.Up, "Up"))
        .Or(KeyCommandFor(TapeToken.Down, "Down"))
        .Or(KeyCommandFor(TapeToken.Left, "Left"))
        .Or(KeyCommandFor(TapeToken.Right, "Right"))
        .Or(KeyCommandFor(TapeToken.PageUp, "PageUp"))
        .Or(KeyCommandFor(TapeToken.PageDown, "PageDown"))
        .Or(KeyCommandFor(TapeToken.Home, "Home"))
        .Or(KeyCommandFor(TapeToken.End, "End"));

    // Modifier command: Ctrl+C, Alt+Enter, etc.
    private static readonly TokenListParser<TapeToken, string> ModifierKey =
        Token.EqualTo(TapeToken.Enter).Value("Enter")
        .Or(Token.EqualTo(TapeToken.Tab).Value("Tab"))
        .Or(Token.EqualTo(TapeToken.Space).Value("Space"))
        .Or(Token.EqualTo(TapeToken.Escape).Value("Escape"))
        .Or(Token.EqualTo(TapeToken.Backspace).Value("Backspace"))
        .Or(Token.EqualTo(TapeToken.Delete).Value("Delete"))
        .Or(Identifier);

    // Helper to parse modifier combinations - accepts ANY order of Ctrl/Alt/Shift
    private static readonly TokenListParser<TapeToken, ICommand> ModifierCommand =
        Parse.Ref(() =>
        {
            // Parser that accepts modifiers in any order
            // Examples: Ctrl+C, Alt+Enter, Ctrl+Alt+Shift+Tab, Shift+Ctrl+C

            var plus = Token.EqualTo(TapeToken.Plus);

            // Parse a single modifier and return which one it is
            var singleModifier =
                Token.EqualTo(TapeToken.Ctrl).Value("Ctrl")
                .Or(Token.EqualTo(TapeToken.Alt).Value("Alt"))
                .Or(Token.EqualTo(TapeToken.Shift).Value("Shift"));

            // Parse modifier followed by +
            var modifierWithPlus =
                from mod in singleModifier
                from p in plus
                select mod;

            // Collect all modifiers (one or more)
            var modifiers =
                from mods in modifierWithPlus.AtLeastOnce()
                from key in ModifierKey
                select (mods.ToList(), key);

            // Convert modifier list to flags and create command
            return modifiers.Select(tuple =>
            {
                var (modList, key) = tuple;
                var hasCtrl = modList.Contains("Ctrl");
                var hasAlt = modList.Contains("Alt");
                var hasShift = modList.Contains("Shift");
                return (ICommand)new ModifierCommand(hasCtrl, hasAlt, hasShift, key);
            });
        });


    // Wait command: Wait, Wait+Screen, Wait+Buffer@10ms /pattern/
    // Also accepts "Wait" as identifier to handle tokenizer ambiguity (e.g., "Wait" vs "WaitTimeout")
    private static readonly TokenListParser<TapeToken, ICommand> WaitCommand =
        from keyword in Token.EqualTo(TapeToken.Wait).Or(Token.EqualTo(TapeToken.Identifier).Where(t => t.ToStringValue().Equals("Wait", StringComparison.OrdinalIgnoreCase)))
        from scope in Token.EqualTo(TapeToken.PlusScreen).Value(WaitScope.Screen)
            .Or(Token.EqualTo(TapeToken.PlusBuffer).Value(WaitScope.Buffer))
            .Or(Token.EqualTo(TapeToken.PlusLine).Value(WaitScope.Line))
            .OptionalOrDefault(WaitScope.Buffer)
        from timeout in SpeedModifier.OptionalOrDefault()
        from pattern in RegexPattern.OptionalOrDefault()
        select (ICommand)new WaitCommand(
            scope,
            timeout == TimeSpan.Zero ? null : timeout,
            pattern);

    // Hide command: Hide
    private static readonly TokenListParser<TapeToken, ICommand> HideCommand =
        from keyword in Token.EqualTo(TapeToken.Hide)
        select (ICommand)new HideCommand();

    // Show command: Show
    private static readonly TokenListParser<TapeToken, ICommand> ShowCommand =
        from keyword in Token.EqualTo(TapeToken.Show)
        select (ICommand)new ShowCommand();

    // Screenshot command: Screenshot test.png
    private static readonly TokenListParser<TapeToken, ICommand> ScreenshotCommand =
        from keyword in Token.EqualTo(TapeToken.Screenshot)
        from path in FilePath
        select (ICommand)new ScreenshotCommand(path);

    // Copy command: Copy "text"
    private static readonly TokenListParser<TapeToken, ICommand> CopyCommand =
        from keyword in Token.EqualTo(TapeToken.Copy)
        from text in QuotedString
        select (ICommand)new CopyCommand(text);

    // Paste command: Paste
    private static readonly TokenListParser<TapeToken, ICommand> PasteCommand =
        from keyword in Token.EqualTo(TapeToken.Paste)
        select (ICommand)new PasteCommand();

    // Env command: Env KEY "value"
    private static readonly TokenListParser<TapeToken, ICommand> EnvCommand =
        from keyword in Token.EqualTo(TapeToken.Env)
        from key in Identifier
        from value in QuotedString
        select (ICommand)new EnvCommand(key, value);

    // Exec command: Exec "ls -la"
    private static readonly TokenListParser<TapeToken, ICommand> ExecCommand =
        from keyword in Token.EqualTo(TapeToken.Exec)
        from command in QuotedString
        select (ICommand)new ExecCommand(command);

    // Combined command parser - order matters!
    private static readonly TokenListParser<TapeToken, ICommand> Command =
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
    private static readonly TokenListParser<TapeToken, ICommand[]> CommandLine =
        Command.Many();

    // Full tape file parser
    private static readonly TokenListParser<TapeToken, ICommand[]> TapeFile =
        from commands in CommandLine
        select commands;

    /// <summary>
    /// All valid setting names for SET commands (case-insensitive).
    /// </summary>
    private static readonly HashSet<string> ValidSettingNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Terminal dimensions
        "Width", "Height", "Cols", "Rows",

        // Font settings
        "FontSize", "FontFamily", "LetterSpacing", "LineHeight",

        // Video settings
        "Framerate", "PlaybackSpeed", "LoopOffset", "MaxColors",

        // Styling
        "Theme", "Padding", "Margin", "MarginFill", "WindowBarSize",
        "BorderRadius", "CursorBlink", "TransparentBackground",

        // Behavior
        "Shell", "WorkingDirectory", "TypingSpeed", "WaitTimeout",
        "WaitPattern", "InactivityTimeout", "StartWaitTimeout",
        "StartBuffer", "EndBuffer", "StartupDelay"
    };

    /// <summary>
    /// Calculates Levenshtein distance between two strings for fuzzy matching.
    /// </summary>
    private static int LevenshteinDistance(string s1, string s2)
    {
        var len1 = s1.Length;
        var len2 = s2.Length;
        var d = new int[len1 + 1, len2 + 1];

        for (var i = 0; i <= len1; i++) d[i, 0] = i;
        for (var j = 0; j <= len2; j++) d[0, j] = j;

        for (var i = 1; i <= len1; i++)
        {
            for (var j = 1; j <= len2; j++)
            {
                var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[len1, len2];
    }

    /// <summary>
    /// Finds the closest matching valid setting name for fuzzy matching suggestions.
    /// </summary>
    private static string? FindClosestSettingName(string invalidName)
    {
        var invalidLower = invalidName.ToLower();

        // First, check for prefix matches (e.g., "Font" -> "FontFamily" or "FontSize")
        var prefixMatches = ValidSettingNames
            .Where(validName => validName.StartsWith(invalidLower, StringComparison.CurrentCultureIgnoreCase))
            .ToList();

        if (prefixMatches.Count > 0)
        {
            // Return shortest prefix match (most likely intended)
            return prefixMatches.OrderBy(x => x.Length).First();
        }

        // If no prefix match, use Levenshtein distance
        var bestMatch = ValidSettingNames
            .Select(validName => new
            {
                Name = validName,
                Distance = LevenshteinDistance(invalidLower, validName.ToLower())
            })
            .OrderBy(x => x.Distance)
            .ThenBy(x => Math.Abs(x.Name.Length - invalidName.Length)) // Prefer similar length
            .FirstOrDefault();

        // Only suggest if distance is reasonably small (within 3 edits)
        return bestMatch?.Distance <= 3 ? bestMatch.Name : null;
    }

    /// <summary>
    /// Validates SET command usage rules:
    /// 1. SET commands can only appear once per setting name
    /// 2. SET commands must appear before action commands
    /// 3. SET commands must use valid setting names
    /// </summary>
    private static void ValidateSetCommands(List<ICommand> commands)
    {
        var seenSettings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var actionCommandEncountered = false;

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
                var lineNumber = setCommand.LineNumber;

                // Check for invalid setting names
                if (!ValidSettingNames.Contains(setCommand.SettingName))
                {
                    var errorMessage = $"Unknown setting name '{setCommand.SettingName}'";
                    var suggestion = FindClosestSettingName(setCommand.SettingName);
                    if (suggestion != null)
                    {
                        errorMessage += $". Did you mean '{suggestion}'?";
                    }

                    throw new TapeParseException(
                        errorMessage,
                        null,
                        lineNumber,
                        1);
                }

                // Check for duplicate SET commands
                if (seenSettings.Contains(setCommand.SettingName))
                {
                    throw new TapeParseException(
                        $"Duplicate SET command: '{setCommand.SettingName}' has already been set",
                        null,
                        lineNumber,
                        1);
                }

                // Check if SET appears after action commands
                if (actionCommandEncountered)
                {
                    throw new TapeParseException(
                        $"SET command for '{setCommand.SettingName}' appears after action commands. All SET commands must appear before Type, Exec, Sleep, Wait, and other action commands",
                        null,
                        lineNumber,
                        1);
                }

                seenSettings.Add(setCommand.SettingName);
            }
        }
    }

    /// <summary>
    /// Parses a tape file and returns a list of commands.
    /// </summary>
    public List<ICommand> ParseTape(string source, string? filePath = null)
    {
        try
        {
            var tokenList = Tokenizer.Tokenize(source);
            var result = TapeFile.AtEnd().Parse(tokenList);
            var commandList = result.ToList();

            // Validate SET command rules
            ValidateSetCommands(commandList);

            return commandList;
        }
        catch (ParseException ex)
        {
            // Extract position information from ParseException
            var line = 1;
            var column = 1;

            if (ex.ErrorPosition.HasValue)
            {
                line = ex.ErrorPosition.Line;
                column = ex.ErrorPosition.Column;
            }

            // Use Superpower's error message as-is for better context
            var errorMessage = ex.Message;

            throw new TapeParseException(
                errorMessage,
                ex,
                line,
                column);
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
public class TapeParseException(string message, Exception? innerException, int line = 0, int column = 0)
    : Exception(message, innerException)
{
    public int Line { get; } = line;
    public int Column { get; } = column;
}
