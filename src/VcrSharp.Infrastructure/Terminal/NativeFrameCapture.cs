using System.Diagnostics;
using VcrSharp.Core.Rendering;
using VcrSharp.Core.Session;
using VcrSharp.Infrastructure.Rendering;

namespace VcrSharp.Infrastructure.Terminal;

/// <summary>
/// <see cref="IFrameCapture"/> for the native (browserless) path. <c>Screenshot</c> snapshots the live
/// grid and renders it with the existing <see cref="SvgRenderer"/> (SVG only — there is no rasteriser
/// in this path yet); the buffer-settle poll reads the grid directly.
/// </summary>
public sealed class NativeFrameCapture(NativeTerminalPage page, SessionOptions options) : IFrameCapture
{
    public async Task CaptureScreenshotAsync(string filePath)
    {
        if (!filePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException(
                $"Native recording can only screenshot to .svg (got '{Path.GetFileName(filePath)}').");

        var content = page.Snapshot();

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var renderer = new SvgRenderer(options);
        if (options.FitToContent)
        {
            var extent = ContentExtent.Measure(content);
            renderer.SetContentExtent(extent.Cols, extent.Rows);
        }
        await renderer.RenderStaticAsync(filePath, content);
    }

    public Task WaitForBufferStableAsync(TimeSpan inactivityTimeout, TimeSpan maxWait,
        CancellationToken cancellationToken = default)
        => WaitForBufferStableAsync(inactivityTimeout, maxWait, TimeSpan.Zero, cancellationToken);

    /// <summary><paramref name="minWait"/> guards against settling on the pre-output prompt while an
    /// Exec command (e.g. <c>dotnet run</c>) is still starting up — stability can't be declared before it.</summary>
    public async Task WaitForBufferStableAsync(TimeSpan inactivityTimeout, TimeSpan maxWait,
        TimeSpan minWait, CancellationToken cancellationToken = default)
    {
        var overall = Stopwatch.StartNew();
        var sinceChange = Stopwatch.StartNew();
        var last = Signature();

        while (overall.Elapsed < maxWait)
        {
            await Task.Delay(20, cancellationToken);
            var now = Signature();
            if (now != last) { last = now; sinceChange.Restart(); }
            else if (overall.Elapsed >= minWait && sinceChange.Elapsed >= inactivityTimeout) return;
        }
    }

    private string Signature() => NativeTerminalRenderer.Signature(page.Snapshot());
}
