using Microsoft.Playwright;
using VcrSharp.Core.Logging;

namespace VcrSharp.Infrastructure.Playwright;

/// <summary>
/// Manages Playwright browser lifecycle for terminal recording.
/// </summary>
public class PlaywrightBrowser : IDisposable
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private bool _disposed;

    /// <summary>
    /// Gets whether the browser is currently running.
    /// </summary>
    public bool IsRunning => _browser is { IsConnected: true };

    /// <summary>
    /// Gets the browser context (required for CDP session creation).
    /// </summary>
    public IBrowserContext? Context => _context;

    /// <summary>
    /// Ensures Playwright browsers and drivers are installed. Automatically installs/updates if needed.
    /// </summary>
    /// <param name="progress">Optional progress callback for reporting installation status.</param>
    /// <returns>A task representing the async operation.</returns>
    public static async Task EnsureBrowsersInstalled(Action<string>? progress = null)
    {
        // Check if Playwright drivers are available and working
        // This catches both missing drivers AND version mismatches from upgrades
        var driversAvailable = await AreDriversAvailable();

        if (!driversAvailable)
        {
            // Drivers missing or incompatible - install/update them
            // Running "playwright install" will install both drivers and browsers
            progress?.Invoke("Installing Playwright drivers and Chromium browser (this may take 10-60 seconds)...");

            var exitCode = Program.Main(["install", "chromium", "--no-shell"]);

            if (exitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Failed to install Playwright (exit code: {exitCode}). " +
                    "Please ensure you have internet connectivity and sufficient disk space.");
            }

            // Verify drivers are now working after installation
            var driversNowAvailable = await AreDriversAvailable();
            if (!driversNowAvailable)
            {
                throw new InvalidOperationException(
                    "Playwright installation completed, but drivers are still not available. " +
                    "This may indicate a build or packaging issue. Try rebuilding the project:\n" +
                    "  dotnet clean\n" +
                    "  dotnet build");
            }

            progress?.Invoke("Playwright installed successfully");
            return;
        }

        // Drivers are working - check if browsers need installation
        // Playwright stores browsers in:
        // - Windows: %USERPROFILE%\AppData\Local\ms-playwright
        // - Linux/Mac: ~/.cache/ms-playwright
        var playwrightHome = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        if (string.IsNullOrEmpty(playwrightHome))
        {
            playwrightHome = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                OperatingSystem.IsWindows() ? @"AppData\Local\ms-playwright" : ".cache/ms-playwright"
            );
        }

        // Check if any chromium directory exists
        var browsersInstalled = Directory.Exists(playwrightHome) &&
                                Directory.GetDirectories(playwrightHome, "chromium*").Length > 0;

        if (!browsersInstalled)
        {
            // Browsers not installed - install them
            progress?.Invoke("Installing Chromium browser (this may take 30-60 seconds)...");

            var exitCode = Program.Main(["install", "chromium", "--no-shell"]);

            if (exitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Failed to install Playwright browsers (exit code: {exitCode}). " +
                    "Please ensure you have internet connectivity and sufficient disk space.");
            }

            progress?.Invoke("Chromium installed successfully");
        }
    }

    /// <summary>
    /// Checks if Playwright driver files are available and working.
    /// Attempts to create a Playwright instance to verify drivers are functional.
    /// </summary>
    /// <returns>True if drivers are available and working, false otherwise.</returns>
    private static async Task<bool> AreDriversAvailable()
    {
        try
        {
            // Try to create a Playwright instance - this will fail if drivers are missing or incompatible
            // This is the most accurate test because it's exactly what we'll do later
            using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
            return true;
        }
        catch (PlaywrightException ex) when (ex.Message.Contains("Driver not found") ||
                                              ex.Message.Contains("missing required assets"))
        {
            // Drivers are missing or incompatible
            return false;
        }
        catch
        {
            // Some other error - assume drivers are not working
            return false;
        }
    }

    /// <summary>
    /// Launches a Chromium browser instance.
    /// </summary>
    /// <param name="headless">Whether to run in headless mode (default: true).</param>
    /// <param name="slowMo">Slows down operations by specified milliseconds (for debugging).</param>
    /// <returns>A task representing the async operation.</returns>
    public async Task LaunchAsync(bool headless = true, float slowMo = 0)
    {
        if (_browser != null)
        {
            throw new InvalidOperationException("Browser is already launched.");
        }

        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();

        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = headless,
            Channel = "chromium",
            SlowMo = slowMo,
            Args =
            [
                "--no-sandbox",
                "--disable-setuid-sandbox",
                "--disable-dev-shm-usage",
                "--disable-web-security" // Allow CORS for local ttyd
            ]
        });
    }

    /// <summary>
    /// Creates a new browser page and navigates to the specified URL.
    /// Creates a browser context on first call for isolated session with proper input handling.
    /// </summary>
    /// <param name="url">The URL to navigate to (typically ttyd's local server).</param>
    /// <param name="width">Viewport width in pixels.</param>
    /// <param name="height">Viewport height in pixels.</param>
    /// <param name="padding">Padding in pixels to subtract from viewport (padding will be added back during rendering).</param>
    /// <param name="cols">Optional terminal columns. If specified, viewport width will be auto-calculated.</param>
    /// <param name="rows">Optional terminal rows. If specified, viewport height will be auto-calculated.</param>
    /// <param name="fontSize">Font size in pixels, used to estimate initial viewport for cols/rows.</param>
    /// <returns>A new IPage instance.</returns>
    public async Task<IPage> NewPageAsync(string url, int width = 1200, int height = 600, int padding = 0, int? cols = null, int? rows = null, int fontSize = 22)
    {
        if (_browser == null)
        {
            throw new InvalidOperationException("Browser has not been launched. Call LaunchAsync first.");
        }

        // Create browser context if not already created
        // Context provides isolated session with proper input event handling
        if (_context == null)
        {
            int viewportWidth;
            int viewportHeight;

            // If Cols/Rows specified, estimate initial viewport based on fontSize
            // Use fontSize * 0.6 for width (typical monospace ratio) and fontSize * 1.2 for height
            // This will be refined after measuring actual cell dimensions
            if (cols.HasValue || rows.HasValue)
            {
                // Estimate cell dimensions from fontSize
                // Typical monospace fonts: width ≈ 0.6 * fontSize, height ≈ 1.2 * fontSize
                var estimatedCellWidth = fontSize * 0.6;
                var estimatedCellHeight = fontSize * 1.2;

                viewportWidth = cols.HasValue ? (int)Math.Ceiling(cols.Value * estimatedCellWidth) : width;
                viewportHeight = rows.HasValue ? (int)Math.Ceiling(rows.Value * estimatedCellHeight) : height;
            }
            else
            {
                // Subtract padding from viewport dimensions (padding will be added back during FFmpeg rendering)
                // This matches VHS behavior: smaller terminal is captured, then padding is added in post-processing
                viewportWidth = width - 2 * padding;
                viewportHeight = height - 2 * padding;
            }

            _context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize
                {
                    Width = viewportWidth,
                    Height = viewportHeight
                },
                IgnoreHTTPSErrors = true,
                Permissions = ["clipboard-read", "clipboard-write"]
            });
        }

        // Create page from context instead of directly from browser
        var page = await _context.NewPageAsync();

        // Navigate to the URL
        await page.GotoAsync(url, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 30000 // 30 seconds timeout
        });

        return page;
    }

    /// <summary>
    /// Closes the browser gracefully.
    /// </summary>
    public async Task CloseAsync()
    {
        if (_context != null)
        {
            await _context.CloseAsync();
            _context = null;
        }

        if (_browser != null)
        {
            await _browser.CloseAsync();
            _browser = null;
        }

        if (_playwright != null)
        {
            _playwright.Dispose();
            _playwright = null;
        }
    }

    /// <summary>
    /// Disposes the browser resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Synchronous cleanup - best effort
        try
        {
            _context?.CloseAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            // Log errors during browser context disposal
            VcrLogger.Logger.Debug(ex, "Error closing Playwright browser context during disposal. Error: {ErrorMessage}", ex.Message);
        }

        try
        {
            _browser?.CloseAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            // Log errors during browser disposal
            VcrLogger.Logger.Debug(ex, "Error closing Playwright browser during disposal. Error: {ErrorMessage}", ex.Message);
        }

        _playwright?.Dispose();
        _context = null;
        _browser = null;
        _playwright = null;
        _disposed = true;

        GC.SuppressFinalize(this);
    }
}