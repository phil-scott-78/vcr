namespace VcrSharp.Core.Parsing.Ast;

/// <summary>
/// Interface for all tape commands.
/// </summary>
public interface ICommand
{
    /// <summary>
    /// Executes the command within the given context.
    /// </summary>
    Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken = default);
}