using VcrSharp.Core.Session;

namespace VcrSharp.Core.Parsing.Ast;

/// <summary>
/// Context passed to commands during execution.
/// Contains references to browser, session options, and runtime state.
/// </summary>
public class ExecutionContext(SessionOptions options, SessionState state)
{
    /// <summary>
    /// Session options containing all configuration settings.
    /// </summary>
    public SessionOptions Options { get; } = options;

    /// <summary>
    /// Terminal page instance for browser interaction.
    /// Set at runtime to an implementation from Infrastructure layer.
    /// </summary>
    public ITerminalPage? Page { get; set; }

    /// <summary>
    /// Frame capture controller for recording.
    /// Set at runtime to an implementation from Infrastructure layer.
    /// </summary>
    public IFrameCapture? FrameCapture { get; set; }

    /// <summary>
    /// Runtime state for command execution.
    /// </summary>
    public SessionState State { get; } = state;

    /// <summary>
    /// Gets the terminal page for command execution.
    /// </summary>
    /// <returns>The terminal page instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if Page is not initialized.</exception>
    public ITerminalPage GetTerminalPage()
    {
        if (Page == null)
            throw new InvalidOperationException("TerminalPage not initialized");
        return Page;
    }

    /// <summary>
    /// Gets the frame capture for screenshot operations.
    /// </summary>
    /// <returns>The frame capture instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if FrameCapture is not initialized.</exception>
    public IFrameCapture GetFrameCapture()
    {
        if (FrameCapture == null)
            throw new InvalidOperationException("FrameCapture not initialized");
        return FrameCapture;
    }
}