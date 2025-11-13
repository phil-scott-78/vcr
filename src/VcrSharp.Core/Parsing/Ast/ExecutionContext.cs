using VcrSharp.Core.Session;

namespace VcrSharp.Core.Parsing.Ast;

/// <summary>
/// Context passed to commands during execution.
/// Contains references to browser, session options, and runtime state.
/// </summary>
public class ExecutionContext(SessionOptions options, SessionState state, ITerminalPage page, IFrameCapture frameCapture)
{
    /// <summary>
    /// Session options containing all configuration settings.
    /// </summary>
    public SessionOptions Options { get; } = options;

    /// <summary>
    /// Terminal page instance for browser interaction.
    /// Set at runtime to an implementation from Infrastructure layer.
    /// </summary>
    public ITerminalPage Page { get;  } = page;

    /// <summary>
    /// Frame capture controller for recording.
    /// Set at runtime to an implementation from Infrastructure layer.
    /// </summary>
    public IFrameCapture FrameCapture { get;  } = frameCapture;

    /// <summary>
    /// Runtime state for command execution.
    /// </summary>
    public SessionState State { get; } = state;
}