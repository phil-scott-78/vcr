using System.Text.RegularExpressions;
using VcrSharp.Core.Logging;

namespace VcrSharp.Core.Parsing.Ast;

/// <summary>
/// Scope for Wait command - where to look for the pattern.
/// </summary>
public enum WaitScope
{
    /// <summary>
    /// Wait for pattern in current line only.
    /// </summary>
    Line,

    /// <summary>
    /// Wait for pattern in entire screen.
    /// </summary>
    Screen,

    /// <summary>
    /// Wait for pattern in persistent buffer (accumulating content).
    /// This scope maintains state across Wait commands to prevent missing fast-scrolling content.
    /// </summary>
    Buffer
}

/// <summary>
/// Represents a Wait command with optional scope, timeout, and pattern.
/// Examples:
/// - Wait
/// - Wait+Buffer
/// - Wait+Screen
/// - Wait+Line
/// - Wait@10ms
/// - Wait /pattern/
/// - Wait+Screen /pattern/
/// - Wait@100ms /pattern/
/// - Wait+Buffer@10ms /pattern/
/// </summary>
public class WaitCommand(WaitScope scope = WaitScope.Buffer, TimeSpan? timeout = null, Regex? pattern = null)
    : ICommand
{
    public WaitScope Scope { get; } = scope;
    public TimeSpan? Timeout { get; } = timeout;
    public Regex? Pattern { get; } = pattern;

    public async Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        var page = context.Page;

        // Determine timeout (use override if specified, otherwise use session default)
        var timeout = Timeout ?? context.Options.WaitTimeout;

        // Determine pattern (use specified pattern or session default)
        var pattern = Pattern ?? context.Options.WaitPattern;

        // Determine scope string for logging
        var scopeStr = Scope switch
        {
            WaitScope.Screen => "screen",
            WaitScope.Line => "line",
            _ => "buffer"
        };

        VcrLogger.Logger.Debug("WaitCommand: Starting wait for pattern '{Pattern}' (regex: '{RegexPattern}') in {Scope} with {TimeoutMs}ms timeout (explicit: {HasExplicitPattern}/{HasExplicitTimeout})",
            pattern, pattern.ToString(), scopeStr, (int)timeout.TotalMilliseconds, Pattern != null, Timeout.HasValue);

        bool matched;

        // Use appropriate wait method based on scope
        if (Scope == WaitScope.Buffer)
        {
            // Buffer scope: use persistent buffer with delta detection
            // Explicitly type the result to avoid dynamic typing issues with tuple deconstruction
            (bool, string, string) result = await page.WaitForPatternInPersistentBufferAsync(
                pattern,
                context.State.PersistentBuffer,
                context.State.LastBufferSnapshot,
                (int)timeout.TotalMilliseconds,
                cancellationToken);

            var (isMatched, updatedBuffer, updatedSnapshot) = result;
            matched = isMatched;
            // Update state with modified buffer values
            context.State.PersistentBuffer = updatedBuffer;
            context.State.LastBufferSnapshot = updatedSnapshot;
        }
        else
        {
            // Line or Screen scope: use traditional method
            matched = await page.WaitForPatternAsync(pattern, scopeStr, (int)timeout.TotalMilliseconds, cancellationToken);

            // Clear the persistent buffer when using Line or Screen scopes
            context.State.PersistentBuffer = string.Empty;
            context.State.LastBufferSnapshot = string.Empty;
        }

        if (!matched)
        {
            VcrLogger.Logger.Warning("WaitCommand: Timed out after {TimeoutSeconds}s waiting for pattern '{Pattern}' in {Scope}",
                timeout.TotalSeconds, pattern, scopeStr);
            throw new TimeoutException($"Wait command timed out after {timeout.TotalSeconds:F1}s waiting for pattern '{pattern}' in {Scope.ToString().ToLower()}");
        }

        VcrLogger.Logger.Debug("WaitCommand: Pattern '{Pattern}' matched successfully in {Scope}", pattern, scopeStr);
    }

    public override string ToString()
    {
        var parts = new List<string> { "Wait" };

        parts[0] += Scope switch
        {
            WaitScope.Screen => "+Screen",
            WaitScope.Line => "+Line",
            WaitScope.Buffer => "+Buffer",
            _ => throw new ArgumentOutOfRangeException()
        };

        if (Timeout.HasValue)
            parts[0] += $"@{Timeout.Value.TotalMilliseconds}ms";

        if (Pattern != null)
            parts.Add($"/{Pattern}/");

        return string.Join(" ", parts);
    }
}