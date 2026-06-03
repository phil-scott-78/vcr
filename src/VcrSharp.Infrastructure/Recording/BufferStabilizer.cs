using VcrSharp.Infrastructure.Playwright;

namespace VcrSharp.Infrastructure.Recording;

/// <summary>
/// Polls the terminal buffer until output stops changing (stabilizes) or a maximum wait elapses.
/// Extracted from VcrSession's inactivity wait so the same settle logic can be reused by the
/// Screenshot settle path and the static-output path. This is a pure poll with no
/// activity-monitor or end-buffer side effects (the caller owns those).
/// </summary>
public static class BufferStabilizer
{
    private const int PollIntervalMs = 50;

    /// <summary>
    /// Waits until the terminal buffer is unchanged for <paramref name="inactivityTimeout"/>,
    /// or until <paramref name="maxWait"/> elapses.
    /// </summary>
    /// <param name="page">Terminal page to poll.</param>
    /// <param name="inactivityTimeout">How long the buffer must be unchanged to be considered settled.</param>
    /// <param name="maxWait">Hard cap on total wait time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="minWait">
    /// Optional minimum time before stability can be declared. Use this when output is expected to
    /// appear after a delay (e.g. an Exec command that starts after StartupDelay) so the poll does
    /// not settle prematurely on the pre-output prompt.
    /// </param>
    /// <returns>True if the buffer stabilized; false if <paramref name="maxWait"/> was reached first.</returns>
    public static async Task<bool> WaitForStableAsync(
        TerminalPage page,
        TimeSpan inactivityTimeout,
        TimeSpan maxWait,
        CancellationToken cancellationToken = default,
        TimeSpan? minWait = null)
    {
        ArgumentNullException.ThrowIfNull(page);

        var startTime = DateTime.UtcNow;
        var minimum = minWait ?? TimeSpan.Zero;
        string? lastContent = null;
        DateTime? lastChangeTime = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var elapsed = DateTime.UtcNow - startTime;
            if (elapsed > maxWait)
                return false;

            var current = await page.GetBufferContentAsync();
            if (current != lastContent)
            {
                lastContent = current;
                lastChangeTime = DateTime.UtcNow;
            }
            else if (elapsed >= minimum
                     && lastChangeTime.HasValue
                     && DateTime.UtcNow - lastChangeTime.Value >= inactivityTimeout)
            {
                return true;
            }

            await Task.Delay(PollIntervalMs, cancellationToken);
        }
    }
}
