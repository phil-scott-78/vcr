namespace VcrSharp.Core.Parsing.Ast;

/// <summary>
/// Represents an Exec command that executes a real shell command (VcrSharp-specific).
/// Example: Exec "npm install", Exec "git status"
/// <para>
/// Also supports a <em>macro form</em>: <c>Exec showcase table</c>, where <c>showcase</c> names a
/// <c>[macro]</c> template in a discovered <c>vcr.toml</c>. The macro form is expanded into a
/// literal Exec by <see cref="Config.PresetResolver"/> before recording; <see cref="IsMacro"/> is
/// always false by the time the command reaches the session.
/// </para>
/// </summary>
public class ExecCommand : ICommand
{
    /// <summary>
    /// The literal shell command (when <see cref="IsMacro"/> is false), or the macro name
    /// (when <see cref="IsMacro"/> is true, prior to resolution).
    /// </summary>
    public string Command { get; }

    /// <summary>True when this is an unresolved macro invocation (<c>Exec name arg</c>).</summary>
    public bool IsMacro { get; }

    /// <summary>Optional single positional argument for the macro form (<c>{0}</c>).</summary>
    public string? MacroArg { get; }

    /// <summary>Source line for diagnostics.</summary>
    public int LineNumber { get; }

    /// <summary>Creates a literal Exec command.</summary>
    public ExecCommand(string command, int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(command);
        Command = command;
        LineNumber = lineNumber;
    }

    private ExecCommand(string macroName, string? macroArg, bool isMacro, int lineNumber)
    {
        ArgumentNullException.ThrowIfNull(macroName);
        Command = macroName;
        MacroArg = macroArg;
        IsMacro = isMacro;
        LineNumber = lineNumber;
    }

    /// <summary>Creates an unresolved macro-form Exec command (<c>Exec name [arg]</c>).</summary>
    public static ExecCommand Macro(string macroName, string? macroArg, int lineNumber = 0) =>
        new(macroName, macroArg, isMacro: true, lineNumber);

    public Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        // Exec is a no-op at runtime: its command is baked into the shell launch line before the
        // command loop runs (see RecordingSession/PresetResolver), not executed here.
        return Task.CompletedTask;
    }

    public override string ToString() =>
        IsMacro
            ? $"Exec {Command}{(MacroArg is null ? "" : $" {MacroArg}")}"
            : $"Exec \"{Command}\"";
}
