using System.Text.RegularExpressions;

namespace VcrSharp.Core.Session;

/// <summary>
/// Interface for terminal page interaction.
/// Defines the contract for commands to interact with the terminal without depending on Infrastructure types.
/// </summary>
public interface ITerminalPage
{
    /// <summary>
    /// Types text into the terminal with a specified delay between characters.
    /// </summary>
    /// <param name="text">The text to type.</param>
    /// <param name="delayMs">Delay in milliseconds between characters.</param>
    Task TypeAsync(string text, int delayMs);

    /// <summary>
    /// Presses a single key in the terminal.
    /// </summary>
    /// <param name="key">The key to press (e.g., "Enter", "ArrowUp").</param>
    Task PressKeyAsync(string key);

    /// <summary>
    /// Presses a key combination with modifiers.
    /// </summary>
    /// <param name="modifiers">List of modifier keys (e.g., "Control", "Alt").</param>
    /// <param name="key">The main key to press.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PressKeyCombinationAsync(List<string> modifiers, string key, CancellationToken cancellationToken);

    /// <summary>
    /// Waits for a pattern to appear in the terminal.
    /// </summary>
    /// <param name="pattern">Regular expression pattern to match.</param>
    /// <param name="scope">Scope to search ("screen", "buffer", or "buffer+screen").</param>
    /// <param name="timeoutMs">Timeout in milliseconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if pattern was found, false if timeout occurred.</returns>
    Task<bool> WaitForPatternAsync(Regex pattern, string scope, int timeoutMs, CancellationToken cancellationToken);

    /// <summary>
    /// Waits for a pattern to appear in the persistent buffer.
    /// </summary>
    /// <param name="pattern">Regular expression pattern to match.</param>
    /// <param name="persistentBuffer">The current persistent buffer content.</param>
    /// <param name="lastSnapshot">The last buffer snapshot for delta detection.</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple of (matched, updatedPersistentBuffer, updatedLastSnapshot).</returns>
    Task<(bool Matched, string UpdatedPersistentBuffer, string UpdatedLastSnapshot)> WaitForPatternInPersistentBufferAsync(
        Regex pattern,
        string persistentBuffer,
        string lastSnapshot,
        int timeout = 15000,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies text to the clipboard.
    /// </summary>
    /// <param name="text">The text to copy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CopyToClipboardAsync(string text, CancellationToken cancellationToken);

    /// <summary>
    /// Pastes text from the clipboard into the terminal.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PasteFromClipboardAsync(CancellationToken cancellationToken);
}