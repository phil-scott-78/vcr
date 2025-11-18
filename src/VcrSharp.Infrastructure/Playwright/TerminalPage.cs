using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using VcrSharp.Core.Logging;
using VcrSharp.Core.Session;

namespace VcrSharp.Infrastructure.Playwright;

/// <summary>
/// Represents terminal dimensions returned from JavaScript.
/// </summary>
internal sealed class TerminalDimensions
{
    [JsonPropertyName("cols")]
    public int Cols { get; set; }

    [JsonPropertyName("rows")]
    public int Rows { get; set; }
}

/// <summary>
/// Represents cell dimensions returned from JavaScript.
/// </summary>
internal sealed class CellDimensions
{
    [JsonPropertyName("cellWidth")]
    public double CellWidth { get; set; }

    [JsonPropertyName("cellHeight")]
    public double CellHeight { get; set; }
}

/// <summary>
/// Represents viewport and canvas information returned from JavaScript.
/// </summary>
internal sealed class ViewportInfo
{
    [JsonPropertyName("viewportWidth")]
    public int ViewportWidth { get; set; }

    [JsonPropertyName("viewportHeight")]
    public int ViewportHeight { get; set; }

    [JsonPropertyName("canvasWidth")]
    public int CanvasWidth { get; set; }

    [JsonPropertyName("canvasHeight")]
    public int CanvasHeight { get; set; }
}

/// <summary>
/// JSON serialization context for trim-safe serialization.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(float))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(TerminalDimensions))]
[JsonSerializable(typeof(CellDimensions))]
[JsonSerializable(typeof(ViewportInfo))]
internal partial class TerminalPageJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Wraps a Playwright page to provide terminal-specific interactions with xterm.js.
/// </summary>
public class TerminalPage : ITerminalPage
{
    private readonly IPage _page;
    private ICDPSession? _cdpSession;

    /// <summary>
    /// Initializes a new instance of the TerminalPage class.
    /// </summary>
    /// <param name="page">The Playwright page to wrap.</param>
    public TerminalPage(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        _page = page;
    }

    /// <summary>
    /// Waits for xterm.js to fully initialize on the page.
    /// Performs comprehensive checks to ensure xterm.js is fully rendered and ready.
    /// </summary>
    /// <param name="timeout">Maximum time to wait in milliseconds (default: 10000ms).</param>
    /// <returns>A task representing the async operation.</returns>
    public async Task WaitForTerminalReadyAsync(int timeout = 10000)
    {
        // Wait for xterm.js terminal element to be present in DOM
        await _page.WaitForSelectorAsync(".xterm", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Attached,
            Timeout = timeout
        });

        // Wait for terminal to be fully initialized with rendering canvas
        // The .xterm-screen element contains the actual terminal canvas
        await _page.WaitForFunctionAsync("""

                                         () => {
                                             const termElement = document.querySelector('.xterm');
                                             return termElement && termElement.querySelector('.xterm-screen') !== null;
                                         }

                                         """, new PageWaitForFunctionOptions
        {
            Timeout = timeout
        });

        // Wait for canvas layers to be present (VHS approach)
        // These are the actual rendering canvases that display terminal content
        await _page.WaitForFunctionAsync("""

                                         () => {
                                             const textCanvas = document.querySelector('canvas.xterm-text-layer');
                                             const cursorCanvas = document.querySelector('canvas.xterm-cursor-layer');
                                             return textCanvas !== null && cursorCanvas !== null;
                                         }

                                         """, new PageWaitForFunctionOptions
        {
            Timeout = timeout
        });

        // Wait for terminal object to be fully initialized
        await _page.WaitForFunctionAsync("""

                                         () => {
                                             return window.term &&
                                                    window.term.buffer &&
                                                    window.term.buffer.active;
                                         }

                                         """, new PageWaitForFunctionOptions
        {
            Timeout = timeout
        });
    }

    /// <summary>
    /// Waits for the shell prompt to appear, indicating the terminal is ready to accept commands.
    /// This polls the terminal content until text appears on the last line (indicating a prompt).
    /// </summary>
    /// <param name="timeout">Maximum time to wait in milliseconds (default: 3000ms).</param>
    /// <returns>A task representing the async operation.</returns>
    public async Task WaitForPromptAsync(int timeout = 3000)
    {
        const int pollInterval = 100;
        var maxAttempts = timeout / pollInterval;

        for (var i = 0; i < maxAttempts; i++)
        {
            // Check if there's any text content on the last line of the terminal buffer
            var hasPrompt = await _page.EvaluateAsync<bool>("""

                                                            () => {
                                                                if (!window.term || !window.term.buffer || !window.term.buffer.active) {
                                                                    return false;
                                                                }

                                                                const buffer = window.term.buffer.active;
                                                                const lastLineIndex = buffer.baseY + buffer.cursorY;
                                                                const lastLine = buffer.getLine(lastLineIndex);

                                                                if (!lastLine) {
                                                                    return false;
                                                                }

                                                                // Check if last line has any non-whitespace content
                                                                const lineText = lastLine.translateToString(true);
                                                                return lineText.trim().length > 0;
                                                            }

                                                            """);

            if (hasPrompt)
            {
                return;
            }

            await Task.Delay(pollInterval);
        }

        // Timeout is not fatal - continue anyway
        // Some shells may not show a prompt immediately
        VcrLogger.Logger.Warning("Timeout waiting for shell prompt after {Timeout}ms, continuing anyway", timeout);
    }

    /// <summary>
    /// Resizes the terminal to specific cols/rows and adjusts viewport to fit exactly.
    /// </summary>
    /// <param name="cols">Terminal columns (character width).</param>
    /// <param name="rows">Terminal rows (character height).</param>
    /// <returns>Tuple of (width, height, cellWidth, cellHeight) representing the final viewport dimensions and measured cell size.</returns>
    public async Task<(int Width, int Height, double CellWidth, double CellHeight)> ResizeTerminalToColsRowsAsync(int? cols, int? rows)
    {
        // Get current dimensions
        var currentDims = await _page.EvaluateAsync<TerminalDimensions>("""
            () => ({
                cols: window.term.cols,
                rows: window.term.rows
            })
        """);

        // Use provided cols/rows or keep current
        var targetCols = cols ?? currentDims.Cols;
        var targetRows = rows ?? currentDims.Rows;

        // Set terminal to exact cols/rows
        await _page.EvaluateAsync($$"""
            () => {
                window.term.resize({{targetCols}}, {{targetRows}});
            }
        """);

        // Wait a moment for terminal to resize
        await Task.Delay(100);

        // Measure actual cell dimensions
        var cellDimensions = await _page.EvaluateAsync<CellDimensions>("""
            () => {
                // Access xterm.js internal _renderService to get actual cell dimensions
                // This is what xterm.js uses internally for rendering
                const renderService = window.term._core._renderService;
                if (renderService && renderService.dimensions) {
                    return {
                        cellWidth: renderService.dimensions.css.cell.width,
                        cellHeight: renderService.dimensions.css.cell.height
                    };
                }

                // Fallback: measure by inspecting canvas dimensions
                const canvas = document.querySelector('canvas.xterm-text-layer');
                if (canvas && window.term) {
                    return {
                        cellWidth: canvas.width / (window.devicePixelRatio * window.term.cols),
                        cellHeight: canvas.height / (window.devicePixelRatio * window.term.rows)
                    };
                }

                // Last resort: estimate based on fontSize
                return {
                    cellWidth: 0,
                    cellHeight: 0
                };
            }
        """);

        var cellWidth = cellDimensions.CellWidth;
        var cellHeight = cellDimensions.CellHeight;

        VcrLogger.Logger.Debug("Measured cell dimensions: {CellWidth}px × {CellHeight}px", cellWidth, cellHeight);

        // Calculate required canvas size (not viewport - viewport may be larger due to HTML padding/margins)
        var requiredCanvasWidth = (int)Math.Ceiling(targetCols * cellWidth);
        var requiredCanvasHeight = (int)Math.Ceiling(targetRows * cellHeight);

        VcrLogger.Logger.Debug("Required canvas size for {Cols}×{Rows} terminal: {Width}×{Height}",
            targetCols, targetRows, requiredCanvasWidth, requiredCanvasHeight);

        // Query the current viewport to calculate extra space (padding/margins)
        var viewportInfo = await _page.EvaluateAsync<ViewportInfo>("""
            () => {
                const canvas = document.querySelector('canvas.xterm-text-layer');
                return {
                    viewportWidth: window.innerWidth,
                    viewportHeight: window.innerHeight,
                    canvasWidth: canvas ? canvas.clientWidth : 0,
                    canvasHeight: canvas ? canvas.clientHeight : 0
                };
            }
        """);

        // Calculate extra space between viewport and canvas (HTML padding/margins)
        var extraWidth = viewportInfo.ViewportWidth - viewportInfo.CanvasWidth;
        var extraHeight = viewportInfo.ViewportHeight - viewportInfo.CanvasHeight;

        // Required viewport = required canvas + extra space
        var requiredViewportWidth = requiredCanvasWidth + extraWidth;
        var requiredViewportHeight = requiredCanvasHeight + extraHeight;

        // Resize the viewport
        await _page.SetViewportSizeAsync(requiredViewportWidth, requiredViewportHeight);

        // Wait for resize to take effect
        await Task.Delay(100);

        // Return the final viewport dimensions and actual cell dimensions
        return (requiredViewportWidth, requiredViewportHeight, cellWidth, cellHeight);
    }

    /// <summary>
    /// Configures terminal options in a single batch.
    /// </summary>
    /// <param name="options">Dictionary of option names and values to set.</param>
    public async Task ConfigureTerminalAsync(Dictionary<string, object> options)
    {
        // Build JavaScript to set all options at once
        var optionsJson = JsonSerializer.Serialize(options, TerminalPageJsonContext.Default.DictionaryStringObject);

        // Get initial dimensions to detect when re-render completes
        await _page.EvaluateAsync<TerminalDimensions>("""

                                                      () => ({
                                                          cols: window.term.cols,
                                                          rows: window.term.rows
                                                      })

                                                      """);

        // Set options without calling FitAddon (dimensions are always explicit)
        await _page.EvaluateAsync($$"""

                                    (() => {
                                        const opts = {{optionsJson}};
                                        for (const [key, value] of Object.entries(opts)) {
                                            window.term.options[key] = value;
                                        }
                                    })()

                                    """);
    }

    /// <summary>
    /// Sets the terminal theme colors.
    /// </summary>
    /// <param name="theme">Dictionary of color names to hex values.</param>
    public async Task SetThemeAsync(Dictionary<string, string> theme)
    {
        var themeJson = JsonSerializer.Serialize(theme, TerminalPageJsonContext.Default.DictionaryStringString);

        // Set theme and force refresh (no FitAddon - dimensions are always explicit)
        await _page.EvaluateAsync($$"""

                                    (() => {
                                        window.term.options.theme = {{themeJson}};
                                        // Force a render cycle to apply theme immediately
                                        window.term.refresh(0, window.term.rows - 1);
                                    })()

                                    """);

        // Wait for canvas to be ready with new theme by checking canvas context exists
        // This ensures the theme has been applied and rendered
        await _page.WaitForFunctionAsync("""

                                         () => {
                                             const canvas = document.querySelector('canvas.xterm-text-layer');
                                             if (!canvas) return false;
                                             const ctx = canvas.getContext('2d');
                                             return ctx !== null;
                                         }

                                         """, new PageWaitForFunctionOptions { Timeout = 1000 });
    }

    /// <summary>
    /// Loads a Google Font by injecting the necessary link tags into the page head.
    /// Uses the font loading API to ensure the font is fully loaded before proceeding.
    /// </summary>
    /// <param name="fontFamily">The font family name (e.g., "Cascadia Code").</param>
    public async Task LoadGoogleFontAsync(string fontFamily)
    {
        // Add preconnect links for performance
        await _page.EvaluateAsync("""

                                  (() => {
                                      // Preconnect to Google Fonts domains
                                      const preconnect1 = document.createElement('link');
                                      preconnect1.rel = 'preconnect';
                                      preconnect1.href = 'https://fonts.googleapis.com';
                                      document.head.prepend(preconnect1);

                                      const preconnect2 = document.createElement('link');
                                      preconnect2.rel = 'preconnect';
                                      preconnect2.href = 'https://fonts.gstatic.com';
                                      preconnect2.crossOrigin = 'anonymous';
                                      document.head.prepend(preconnect2);
                                  })()

                                  """);

        // Convert font family to Google Fonts URL format
        var fontUrl = $"https://fonts.googleapis.com/css2?family={Uri.EscapeDataString(fontFamily)}:ital,wght@0,200..700;1,200..700&display=swap";

        // Inject Google Fonts stylesheet using Playwright's AddStyleTagAsync
        await _page.AddStyleTagAsync(new PageAddStyleTagOptions
        {
            Url = fontUrl
        });

        // Wait for the font to be loaded using the CSS Font Loading API
        // This ensures the font is ready before we try to use it
        var fontFamilyEscaped = JsonSerializer.Serialize(fontFamily, TerminalPageJsonContext.Default.String);
        await _page.WaitForFunctionAsync($$"""

                                           async () => {
                                               try {
                                                   // Use CSS Font Loading API to check if font is loaded
                                                   await document.fonts.load('12px {{fontFamilyEscaped}}');
                                                   return document.fonts.check('12px {{fontFamilyEscaped}}');
                                               } catch (e) {
                                                   // If font loading fails, return false
                                                   return false;
                                               }
                                           }

                                           """, new PageWaitForFunctionOptions { Timeout = 5000 });
    }

    /// <summary>
    /// Hides the cursor by injecting CSS to hide the cursor canvas layer.
    /// </summary>
    public async Task HideCursorAsync()
    {
        await _page.AddStyleTagAsync(new PageAddStyleTagOptions
        {
            Content = "canvas.xterm-cursor-layer { display: none !important; }"
        });
    }

    /// <summary>
    /// Gets the current terminal buffer content as a string.
    /// </summary>
    /// <returns>The terminal buffer content.</returns>
    public async Task<string> GetBufferContentAsync()
    {
        var content = await _page.EvaluateAsync<string>("""

                                                        () => {
                                                            const term = window.term;
                                                            const buffer = term.buffer.active;
                                                            const lines = [];
                                                            for (let i = 0; i < buffer.length; i++) {
                                                                const line = buffer.getLine(i);
                                                                if (line) {
                                                                    lines.push(line.translateToString(true));
                                                                }
                                                            }
                                                            return lines.join('\n');
                                                        }

                                                        """);

        return content;
    }

    /// <summary>
    /// Gets the last line of the terminal buffer.
    /// </summary>
    /// <returns>The last line content.</returns>
    public async Task<string> GetLastLineAsync()
    {
        var lastLine = await _page.EvaluateAsync<string>("""

                                                                     () => {
                                                                         const term = window.term;
                                                                         const buffer = term.buffer.active;
                                                                         const lineIndex = buffer.viewportY + buffer.cursorY;
                                                                         const line = buffer.getLine(lineIndex);
                                                                         return line ? line.translateToString(true) : '';
                                                                     }

                                                         """);

        return lastLine;
    }

    /// <summary>
    /// Checks if the terminal is still connected and active.
    /// </summary>
    /// <returns>True if terminal is connected, false otherwise.</returns>
    public async Task<bool> IsTerminalConnectedAsync()
    {
        try
        {
            return await _page.EvaluateAsync<bool>("""

                                                                   () => {
                                                                       // Check if terminal object exists
                                                                       if (!window.term) return false;

                                                                       // Check if page/terminal is visible and not disconnected
                                                                       if (document.visibilityState === 'hidden') return false;

                                                                       // If ttyd uses WebSocket, check its state
                                                                       // Note: ttyd may not expose socket directly, so this is optional
                                                                       if (window.term.socket) {
                                                                           return window.term.socket.readyState === WebSocket.OPEN ||
                                                                                  window.term.socket.readyState === WebSocket.CONNECTING;
                                                                       }

                                                                       // If no socket check available, terminal object existence indicates connection
                                                                       return true;
                                                                   }

                                                   """);
        }
        catch
        {
            // If evaluation fails, terminal is likely disconnected
            return false;
        }
    }

    /// <summary>
    /// Waits for the terminal to disconnect (script/shell exits).
    /// </summary>
    /// <param name="timeout">Maximum time to wait in milliseconds.</param>
    /// <param name="cancellationToken">Cancellation token to abort the wait.</param>
    /// <returns>True if terminal disconnected, false if timeout occurred.</returns>
    public async Task<bool> WaitForDisconnectAsync(int timeout = 30000, CancellationToken cancellationToken = default)
    {
        var endTime = DateTime.UtcNow.AddMilliseconds(timeout);

        while (DateTime.UtcNow < endTime)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!await IsTerminalConnectedAsync())
            {
                return true;
            }

            await Task.Delay(100, cancellationToken);
        }

        return false;
    }

    /// <summary>
    /// Waits for the terminal buffer to match the specified pattern.
    /// </summary>
    /// <param name="pattern">Regular expression pattern to match.</param>
    /// <param name="scope">Scope of the search: "screen" for visible buffer, "line" for last line only.</param>
    /// <param name="timeout">Maximum time to wait in milliseconds.</param>
    /// <param name="cancellationToken">Cancellation token to abort the wait.</param>
    /// <returns>True if pattern was found, false if timeout occurred.</returns>
    public async Task<bool> WaitForPatternAsync(Regex pattern, string scope = "screen", int timeout = 15000, CancellationToken cancellationToken = default)
    {
        var endTime = DateTime.UtcNow.AddMilliseconds(timeout);
        var attemptCount = 0;

        while (DateTime.UtcNow < endTime)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var content = scope.ToLowerInvariant() == "line"
                ? await GetLastLineAsync()
                : await GetBufferContentAsync();

            attemptCount++;

            if (pattern.IsMatch(content))
            {
                VcrLogger.Logger.Debug("Wait pattern matched after {AttemptCount} attempts", attemptCount);
                return true;
            }

            await Task.Delay(10, cancellationToken); // Poll every 10ms (match VHS performance)
        }

        // Pattern not found - log final state for debugging
        var finalContent = scope.ToLowerInvariant() == "line"
            ? await GetLastLineAsync()
            : await GetBufferContentAsync();
        VcrLogger.Logger.Warning("Wait timeout after {AttemptCount} attempts. Pattern '{Pattern}' not found. Final content: '{Content}'",
            attemptCount, pattern, finalContent.Replace("\n", "\\n"));

        return false;
    }

    /// <summary>
    /// Waits for the persistent buffer to match the specified pattern.
    /// Appends new terminal content to the persistent buffer and trims on match.
    /// </summary>
    /// <param name="pattern">Regular expression pattern to match.</param>
    /// <param name="persistentBuffer">Persistent buffer that accumulates content across Wait commands.</param>
    /// <param name="lastSnapshot">Last snapshot of terminal buffer for delta detection.</param>
    /// <param name="timeout">Maximum time to wait in milliseconds.</param>
    /// <param name="cancellationToken">Cancellation token to abort the wait.</param>
    /// <returns>Tuple of (matched, updatedPersistentBuffer, updatedLastSnapshot).</returns>
    public async Task<(bool Matched, string UpdatedPersistentBuffer, string UpdatedLastSnapshot)> WaitForPatternInPersistentBufferAsync(
        Regex pattern,
        string persistentBuffer,
        string lastSnapshot,
        int timeout = 15000,
        CancellationToken cancellationToken = default)
    {
        var endTime = DateTime.UtcNow.AddMilliseconds(timeout);
        var attemptCount = 0;

        while (DateTime.UtcNow < endTime)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Get current terminal buffer
            var currentBuffer = await GetBufferContentAsync();

            // Detect and append new content (delta)
            if (currentBuffer != lastSnapshot)
            {
                // Find new content by comparing with last snapshot
                string newContent;
                if (string.IsNullOrEmpty(lastSnapshot))
                {
                    // First time - use entire buffer
                    newContent = currentBuffer;
                }
                else if (currentBuffer.StartsWith(lastSnapshot))
                {
                    // Content was appended
                    newContent = currentBuffer[lastSnapshot.Length..];
                }
                else
                {
                    // Buffer changed completely (e.g., scrolled) - use entire buffer
                    newContent = currentBuffer;
                }

                persistentBuffer += newContent;
                lastSnapshot = currentBuffer;

                VcrLogger.Logger.Verbose("WaitForPatternInPersistentBuffer: Appended {NewContentLength} chars. Persistent buffer now {TotalLength} chars",
                    newContent.Length, persistentBuffer.Length);
            }

            attemptCount++;

            // Search for pattern in persistent buffer
            var match = pattern.Match(persistentBuffer);
            if (match.Success)
            {
                // Trim buffer up to end of first match
                var trimPosition = match.Index + match.Length;
                persistentBuffer = persistentBuffer[trimPosition..];

                VcrLogger.Logger.Debug("Wait pattern matched after {AttemptCount} attempts. Trimmed buffer to {RemainingLength} chars",
                    attemptCount, persistentBuffer.Length);
                return (true, persistentBuffer, lastSnapshot);
            }

            await Task.Delay(10, cancellationToken); // Poll every 10ms
        }

        // Pattern not found
        VcrLogger.Logger.Warning("Wait timeout after {AttemptCount} attempts. Pattern '{Pattern}' not found in persistent buffer ({BufferLength} chars)",
            attemptCount, pattern, persistentBuffer.Length);

        return (false, persistentBuffer, lastSnapshot);
    }

    /// <summary>
    /// Gets the text content of the terminal's text layer canvas.
    /// </summary>
    /// <returns>Locator for the text layer canvas.</returns>
    public ILocator GetTextLayerCanvas()
    {
        return _page.Locator(".xterm-text-layer");
    }

    /// <summary>
    /// Gets the cursor layer canvas.
    /// </summary>
    /// <returns>Locator for the cursor layer canvas.</returns>
    public ILocator GetCursorLayerCanvas()
    {
        return _page.Locator(".xterm-cursor-layer");
    }

    /// <summary>
    /// Captures a canvas element as PNG bytes using GPU-accelerated canvas.toBlob().
    /// Uses async blob API which is more efficient than toDataURL() (no base64 overhead).
    /// </summary>
    /// <param name="selector">CSS selector for the canvas element.</param>
    /// <returns>PNG image bytes.</returns>
    private async Task<byte[]> CaptureCanvasLayerAsync(string selector)
    {
        var canvas = _page.Locator(selector);

        // Use canvas.toBlob() for maximum performance (MDN recommended, more efficient than toDataURL)
        var base64Data = await canvas.EvaluateAsync<string>("""

                                                            canvas => new Promise(resolve => {
                                                                canvas.toBlob(blob => {
                                                                    const reader = new FileReader();
                                                                    reader.onloadend = () => {
                                                                        // Extract base64 data without 'data:image/png;base64,' prefix
                                                                        resolve(reader.result.split(',')[1]);
                                                                    };
                                                                    reader.readAsDataURL(blob);
                                                                }, 'image/png');
                                                            })

                                                            """);

        return Convert.FromBase64String(base64Data);
    }

    /// <summary>
    /// Creates a minimal transparent 1x1 PNG image.
    /// Used when cursor layer should not be rendered (DisableCursor option).
    /// </summary>
    private static byte[] CreateTransparentPng()
    {
        using var image = new Image<Rgba32>(1, 1);
        image[0, 0] = new Rgba32(0, 0, 0, 0); // Fully transparent pixel
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Initializes a CDP session for optimized screenshot capture.
    /// Should be called once after terminal is ready.
    /// </summary>
    public async Task InitializeCdpSessionAsync()
    {
        if (_cdpSession != null)
            return; // Already initialized

        _cdpSession = await _page.Context.NewCDPSessionAsync(_page);
    }

    /// <summary>
    /// Closes the CDP session.
    /// </summary>
    public async Task CloseCdpSessionAsync()
    {
        if (_cdpSession != null)
        {
            await _cdpSession.DetachAsync();
            _cdpSession = null;
        }
    }

    /// <summary>
    /// Captures terminal canvas layers separately and returns them as byte arrays.
    /// This allows deferred compositing in FFmpeg for maximum capture performance.
    /// </summary>
    /// <param name="captureCursor">Whether to capture the cursor layer. If false, returns a transparent cursor layer.</param>
    /// <returns>Tuple of (textLayerBytes, cursorLayerBytes).</returns>
    public async Task<(byte[] TextLayer, byte[] CursorLayer)> CaptureLayersAsync(bool captureCursor = true)
    {
        var textBytes = await CaptureCanvasLayerAsync("canvas.xterm-text-layer");
        var cursorBytes = captureCursor
            ? await CaptureCanvasLayerAsync("canvas.xterm-cursor-layer")
            : CreateTransparentPng();
        return (textBytes, cursorBytes);
    }

    /// <summary>
    /// Takes a screenshot of the terminal area using canvas capture for maximum performance.
    /// Captures text and cursor layers separately using GPU-accelerated canvas.toBlob(),
    /// then composites them. Falls back to CDP/Playwright methods if canvas capture fails.
    /// </summary>
    /// <param name="path">Path to save the screenshot.</param>
    /// <param name="captureCursor">Whether to capture the cursor layer. If false, cursor will not appear in the screenshot.</param>
    public async Task ScreenshotAsync(string path, bool captureCursor = true)
    {
        // Ensure output directory exists
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        try
        {
            // VHS approach: Capture canvas layers directly using toDataURL()
            // This is GPU-accelerated and 2-3x faster than CDP Page.captureScreenshot
            var textBytes = await CaptureCanvasLayerAsync("canvas.xterm-text-layer");
            var cursorBytes = captureCursor
                ? await CaptureCanvasLayerAsync("canvas.xterm-cursor-layer")
                : CreateTransparentPng();

            // Composite the two layers (text + cursor)
            using var textImage = Image.Load<Rgba32>(textBytes);
            using var cursorImage = Image.Load<Rgba32>(cursorBytes);

            // Draw cursor layer on top of text layer
            // ReSharper disable once AccessToDisposedClosure, it is used immediately
            textImage.Mutate(ctx => ctx.DrawImage(cursorImage, new Point(0, 0), 1.0f));

            // Save composited image
            await textImage.SaveAsPngAsync(path);
        }
        catch
        {
            // Fall back to CDP screenshot if canvas capture fails
            byte[]? imageBytes = null;

            if (_cdpSession != null)
            {
                try
                {
                    var response = await _cdpSession.SendAsync("Page.captureScreenshot", new Dictionary<string, object>
                    {
                        ["format"] = "png",
                        ["optimizeForSpeed"] = true
                    });

                    var base64Data = response?.GetProperty("data").GetString();
                    if (base64Data != null)
                    {
                        imageBytes = Convert.FromBase64String(base64Data);
                    }
                }
                catch
                {
                    // we'll fall back to using Playwright's built-in method
                }
            }


            if (imageBytes == null)
            {
                // Fallback to Playwright's built-in method
                var terminal = _page.Locator(".terminal");
                imageBytes = await terminal.ScreenshotAsync();
            }

            await File.WriteAllBytesAsync(path, imageBytes);
        }
    }

    /// <summary>
    /// Types text into the terminal character-by-character with a specified delay between characters.
    /// </summary>
    /// <param name="text">The text to type.</param>
    /// <param name="delayMs">Delay in milliseconds between each character (default: 50ms).</param>
    public async Task TypeAsync(string text, int delayMs = 50)
    {
        VcrLogger.Logger.Debug("TypeAsync: Starting to type {CharCount} characters with {DelayMs}ms delay", text.Length, delayMs);

        // Type character-by-character to ensure proper input handling
        // This matches VHS approach and is more reliable than typing entire strings
        var charIndex = 0;
        foreach (var ch in text)
        {
            try
            {
                VcrLogger.Logger.Verbose("TypeAsync: Typing character #{Index}: '{Char}'", charIndex, ch);
                await _page.Keyboard.TypeAsync(ch.ToString());
                VcrLogger.Logger.Verbose("TypeAsync: Character #{Index} typed successfully", charIndex);

                if (delayMs > 0)
                {
                    await Task.Delay(delayMs);
                }
                charIndex++;
            }
            catch (Exception ex)
            {
                VcrLogger.Logger.Error(ex, "TypeAsync: Failed at character #{Index}: '{Char}'", charIndex, ch);
                throw;
            }
        }

        VcrLogger.Logger.Debug("TypeAsync: Successfully typed all {CharCount} characters", text.Length);
    }

    /// <summary>
    /// Presses a key on the keyboard.
    /// </summary>
    /// <param name="key">The key to press (e.g., "Enter", "Tab", "Escape").</param>
    public async Task PressKeyAsync(string key)
    {
        await _page.Keyboard.PressAsync(key);
    }

    /// <summary>
    /// Presses a key combination with modifiers.
    /// </summary>
    /// <param name="modifiers">List of modifier keys (e.g., "Control", "Alt", "Shift").</param>
    /// <param name="key">The key to press.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task PressKeyCombinationAsync(List<string> modifiers, string key, CancellationToken cancellationToken = default)
    {
        // Check if the key is a character that requires Shift to produce
        // For example: Alt+# should become Alt+Shift+Digit3
        var (physicalKey, requiresShift) = Core.Helpers.KeyMapper.MapCharacterToPhysicalKey(key);

        // Only add implicit Shift for special characters when NOT using Control modifier
        // Control+E should remain Control+KeyE, not Control+Shift+KeyE
        // But Alt+# should become Alt+Shift+Digit3
        var finalModifiers = modifiers.ToList();
        var hasControl = finalModifiers.Contains("Control");

        // For uppercase letters (A-Z), only add Shift if:
        // 1. The character requires shift AND
        // 2. Control is NOT in the modifiers (Control doesn't care about case) AND
        // 3. Shift isn't already present
        var isUppercaseLetter = key is [>= 'A' and <= 'Z'];
        if (requiresShift && !hasControl && !finalModifiers.Contains("Shift"))
        {
            // Only add Shift for non-letter special characters or when Control is not present
            if (!isUppercaseLetter)
            {
                finalModifiers.Add("Shift");
            }
        }

        // Build shortcut string (e.g., "Control+KeyE", "Alt+Shift+Digit3")
        // Playwright expects modifiers + key combined with "+" separator
        var shortcut = string.Join("+", finalModifiers.Append(physicalKey));

        // Send as single shortcut to ensure proper control code is sent
        // instead of visible caret notation (^C, ^E, etc.)
        await _page.Keyboard.PressAsync(shortcut);
    }

    /// <summary>
    /// Clicks the terminal canvas to give it focus.
    /// This is necessary for interactive applications to receive keyboard input.
    /// </summary>
    public async Task ClickTerminalAsync()
    {
        // Click the terminal screen element (parent of all canvas layers) to focus it
        // The cursor layer sits on top, so we click the screen container instead
        var screenSelector = ".xterm .xterm-screen";
        await _page.ClickAsync(screenSelector);
        VcrLogger.Logger.Debug("Clicked terminal screen to give it focus");

        // Small delay to ensure focus is established
        await Task.Delay(100);
    }

    /// <summary>
    /// Copies text to the clipboard using browser APIs.
    /// </summary>
    /// <param name="text">The text to copy.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    public async Task CopyToClipboardAsync(string text, CancellationToken cancellationToken = default)
    {
        var escapedText = JsonSerializer.Serialize(text, TerminalPageJsonContext.Default.String);
        await _page.EvaluateAsync($"navigator.clipboard.writeText({escapedText})");
    }

    /// <summary>
    /// Pastes text from the clipboard.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    public async Task PasteFromClipboardAsync(CancellationToken cancellationToken = default)
    {
        // Simulate Ctrl+V (or Cmd+V on macOS)
        var isMac = await _page.EvaluateAsync<bool>("navigator.platform.toUpperCase().includes('MAC')");
        var modifier = isMac ? "Meta" : "Control";

        await _page.Keyboard.DownAsync(modifier);
        await _page.Keyboard.PressAsync("KeyV");
        await _page.Keyboard.UpAsync(modifier);
    }

    /// <summary>
    /// Reads text from the clipboard using browser APIs.
    /// </summary>
    /// <returns>The clipboard text content.</returns>
    public async Task<string> ReadClipboardAsync()
    {
        var text = await _page.EvaluateAsync<string>("navigator.clipboard.readText()");
        return text;
    }

    /// <summary>
    /// Gets the full terminal content with styling information.
    /// Extracts all cells with character data, colors, and style attributes from the active buffer.
    /// Reads from the viewport offset (viewportY) to capture what's actually visible on screen,
    /// matching PNG screenshot behavior for TUI applications.
    /// </summary>
    /// <returns>Terminal content including text, colors, cursor position, and styles.</returns>
    public async Task<Core.Rendering.TerminalContent> GetTerminalContentWithStylesAsync()
    {
        try
        {
            // JavaScript code to extract terminal content with styles
            // For SVG screenshots, we need to capture what's visually displayed, not just buffer data
            // This means accounting for viewport position and reading the actual visible rows
            var contentJson = await _page.EvaluateAsync<string>("""
                () => {
                    const term = window.term;
                    const buffer = term.buffer.active;
                    const rows = term.rows;
                    const cols = term.cols;

                    // Get viewport information to read from the correct buffer offset
                    const viewportY = buffer.viewportY || 0;
                    const cursorX = buffer.cursorX;
                    const cursorY = buffer.cursorY;

                    const cells = [];

                    // Read from viewport offset to get what's actually visible
                    // viewportY indicates which part of the buffer is currently displayed
                    for (let row = 0; row < rows; row++) {
                        const bufferRow = viewportY + row;
                        const line = buffer.getLine(bufferRow);
                        const rowCells = [];

                        if (line) {
                            for (let col = 0; col < cols; col++) {
                                const cell = line.getCell(col);
                                if (cell) {
                                    const char = cell.getChars() || ' ';
                                    const fg = cell.getFgColor();
                                    const bg = cell.getBgColor();

                                    // Get cell attributes (xterm.js v5+ uses different API)
                                    // These methods return numbers (0 or non-zero), convert to boolean
                                    const isBold = cell.isBold ? (cell.isBold() !== 0) : false;
                                    const isItalic = cell.isItalic ? (cell.isItalic() !== 0) : false;
                                    const isUnderline = cell.isUnderline ? (cell.isUnderline() !== 0) : false;
                                    const isDim = cell.isDim ? (cell.isDim() !== 0) : false;

                                    // Extract colors (xterm.js supports palette and RGB colors)
                                    let fgColor = null;
                                    let bgColor = null;

                                    // Foreground color detection
                                    if (fg !== undefined && fg !== -1) {
                                        // Check color mode using xterm.js API
                                        const isRGB = cell.isFgRGB && cell.isFgRGB();
                                        const isPalette = cell.isFgPalette && cell.isFgPalette();

                                        if (isRGB) {
                                            // RGB color (24-bit)
                                            fgColor = '#' + ('000000' + fg.toString(16)).slice(-6);
                                        } else if (isPalette) {
                                            // Palette color (0-255) - pass as string for SVG mapping
                                            fgColor = fg.toString();
                                        }
                                    }

                                    // Background color detection
                                    if (bg !== undefined && bg !== -1) {
                                        // Check color mode using xterm.js API
                                        if (cell.isBgRGB && cell.isBgRGB()) {
                                            // RGB color (24-bit)
                                            bgColor = '#' + ('000000' + bg.toString(16)).slice(-6);
                                        } else if (cell.isBgPalette && cell.isBgPalette()) {
                                            // Palette color (0-255) - pass as string for SVG mapping
                                            bgColor = bg.toString();
                                        }
                                    }

                                    const isCursor = (row === cursorY && col === cursorX);

                                    rowCells.push({
                                        character: char || ' ',
                                        foregroundColor: fgColor,
                                        backgroundColor: bgColor,
                                        isBold: isBold,
                                        isItalic: isItalic,
                                        isUnderline: isUnderline,
                                        isCursor: isCursor
                                    });
                                }
                            }
                        }

                        cells.push(rowCells);
                    }

                    return JSON.stringify({
                        cols: cols,
                        rows: rows,
                        cursorX: cursorX,
                        cursorY: cursorY,
                        cursorVisible: buffer.cursorHidden === false,
                        cells: cells
                    });
                }
            """);

            if (string.IsNullOrEmpty(contentJson))
            {
                VcrLogger.Logger.Warning("Terminal content JSON is null or empty");
                return new Core.Rendering.TerminalContent();
            }

            // Deserialize the JSON result
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var content = JsonSerializer.Deserialize<Core.Rendering.TerminalContent>(contentJson, options);

            if (content == null)
            {
                VcrLogger.Logger.Warning("Failed to deserialize terminal content (result was null)");
                return new Core.Rendering.TerminalContent();
            }

            if (content.Rows == 0 || content.Cols == 0)
            {
                VcrLogger.Logger.Warning("Terminal content has invalid dimensions: Rows={Rows}, Cols={Cols}", content.Rows, content.Cols);
            }

            return content;
        }
        catch (Exception ex)
        {
            VcrLogger.Logger.Error(ex, "Error capturing terminal content with styles");
            return new Core.Rendering.TerminalContent();
        }
    }
}