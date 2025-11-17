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
        VcrLogger.Logger.Information("Ensuring Playwright browsers and drivers are installed...");

        // Check if Playwright drivers are available and working
        // This catches both missing drivers AND version mismatches from upgrades
        VcrLogger.Logger.Debug("Performing initial driver availability check...");
        var driversAvailable = await AreDriversAvailable();
        VcrLogger.Logger.Information("Initial driver check result: {Available}", driversAvailable ? "Available" : "Not Available");

        if (!driversAvailable)
        {
            // Drivers missing or incompatible - install/update them
            // Running "playwright install" will install both drivers and browsers
            VcrLogger.Logger.Information("Drivers not available - attempting to install via 'playwright install chromium --no-shell'");
            VcrLogger.Logger.Debug("Working directory: {WorkingDirectory}", Directory.GetCurrentDirectory());
            VcrLogger.Logger.Debug("AppContext.BaseDirectory: {BaseDirectory}", AppContext.BaseDirectory);

            progress?.Invoke("Installing Playwright drivers and Chromium browser (this may take 10-60 seconds)...");

            var exitCode = Program.Main(["install", "chromium", "--no-shell"]);

            VcrLogger.Logger.Information("Playwright install command completed with exit code: {ExitCode}", exitCode);

            if (exitCode != 0)
            {
                VcrLogger.Logger.Error("Playwright installation failed with exit code {ExitCode}", exitCode);
                throw new InvalidOperationException(
                    $"Failed to install Playwright (exit code: {exitCode}). " +
                    "Please ensure you have internet connectivity and sufficient disk space.");
            }

            // Verify drivers are now working after installation
            VcrLogger.Logger.Debug("Re-checking driver availability after installation...");
            var driversNowAvailable = await AreDriversAvailable();
            VcrLogger.Logger.Information("Post-installation driver check result: {Available}", driversNowAvailable ? "Available" : "Not Available");

            if (!driversNowAvailable)
            {
                VcrLogger.Logger.Error("Drivers still not available after successful installation - this indicates a packaging or build issue");
                LogPlaywrightEnvironment(); // Log environment again to see what changed (or didn't)
                throw new InvalidOperationException(
                    "Playwright installation completed, but drivers are still not available. " +
                    "This may indicate a build or packaging issue. Try rebuilding the project:\n" +
                    "  dotnet clean\n" +
                    "  dotnet build");
            }

            VcrLogger.Logger.Information("Playwright drivers installed and verified successfully");
            progress?.Invoke("Playwright installed successfully");
            return;
        }

        VcrLogger.Logger.Debug("Drivers are available - checking if browsers need installation...");

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

        VcrLogger.Logger.Debug("Checking for browsers at: {PlaywrightHome}", playwrightHome);

        // Check if any chromium directory exists
        var browsersInstalled = Directory.Exists(playwrightHome) &&
                                Directory.GetDirectories(playwrightHome, "chromium*").Length > 0;

        VcrLogger.Logger.Information("Browsers installed: {Installed}", browsersInstalled);

        if (!browsersInstalled)
        {
            // Browsers not installed - install them
            VcrLogger.Logger.Information("Browsers not found - installing Chromium...");
            progress?.Invoke("Installing Chromium browser (this may take 30-60 seconds)...");

            var exitCode = Program.Main(["install", "chromium", "--no-shell"]);

            VcrLogger.Logger.Information("Browser install command completed with exit code: {ExitCode}", exitCode);

            if (exitCode != 0)
            {
                VcrLogger.Logger.Error("Browser installation failed with exit code {ExitCode}", exitCode);
                throw new InvalidOperationException(
                    $"Failed to install Playwright browsers (exit code: {exitCode}). " +
                    "Please ensure you have internet connectivity and sufficient disk space.");
            }

            VcrLogger.Logger.Information("Chromium browser installed successfully");
            progress?.Invoke("Chromium installed successfully");
        }
        else
        {
            VcrLogger.Logger.Debug("Browsers already installed, skipping installation");
        }

        VcrLogger.Logger.Information("Playwright environment ready - drivers and browsers are available");
    }

    /// <summary>
    /// Logs detailed information about Playwright environment and expected driver locations.
    /// Replicates the logic from Playwright's Driver.GetExecutablePath() to help diagnose issues.
    /// </summary>
    private static void LogPlaywrightEnvironment()
    {
        VcrLogger.Logger.Debug("=== Playwright Environment Diagnostic ===");

        // Log platform information
        var platform = OperatingSystem.IsWindows() ? "Windows" :
                      OperatingSystem.IsMacOS() ? "macOS" :
                      OperatingSystem.IsLinux() ? "Linux" : "Unknown";
        var arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString();
        VcrLogger.Logger.Debug("Platform: {Platform} {Architecture}", platform, arch);

        // Determine the expected node executable name based on platform
        string nodeExecutable = OperatingSystem.IsWindows() ? "node.exe" : "node";
        string platformId = OperatingSystem.IsWindows() ? "win32_x64" :
                           OperatingSystem.IsMacOS() ?
                               (System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64 ? "darwin-arm64" : "darwin-x64") :
                           OperatingSystem.IsLinux() ?
                               (System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64 ? "linux-arm64" : "linux-x64") :
                           "unknown";
        VcrLogger.Logger.Debug("Expected driver platform: {PlatformId}, executable: {NodeExecutable}", platformId, nodeExecutable);

        // Log environment variables that affect Playwright
        var driverSearchPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH");
        var browsersPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        var nodejsPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_NODEJS_PATH");

        VcrLogger.Logger.Debug("PLAYWRIGHT_DRIVER_SEARCH_PATH: {DriverSearchPath}", driverSearchPath ?? "(not set)");
        VcrLogger.Logger.Debug("PLAYWRIGHT_BROWSERS_PATH: {BrowsersPath}", browsersPath ?? "(not set)");
        VcrLogger.Logger.Debug("PLAYWRIGHT_NODEJS_PATH: {NodejsPath}", nodejsPath ?? "(not set)");

        // Log AppContext.BaseDirectory (Playwright's first choice)
        var baseDir = AppContext.BaseDirectory;
        VcrLogger.Logger.Debug("AppContext.BaseDirectory: {BaseDirectory}", baseDir);

        // Check if Microsoft.Playwright.dll exists in BaseDirectory
        var playwrightDllPath = Path.Combine(baseDir, "Microsoft.Playwright.dll");
        var playwrightDllExists = File.Exists(playwrightDllPath);
        VcrLogger.Logger.Debug("Microsoft.Playwright.dll at BaseDirectory: {Exists} ({Path})",
            playwrightDllExists, playwrightDllPath);

        // Log Assembly.Location (Playwright's fallback)
        var assembly = typeof(Microsoft.Playwright.IPlaywright).Assembly;
        VcrLogger.Logger.Debug("Microsoft.Playwright Assembly.Location: {Location}",
            string.IsNullOrEmpty(assembly.Location) ? "(empty - single-file deployment?)" : assembly.Location);

        // Determine which directory Playwright would use (replicating GetExecutablePath logic)
        DirectoryInfo? assemblyDirectory = null;
        if (!string.IsNullOrEmpty(baseDir))
        {
            assemblyDirectory = new DirectoryInfo(baseDir);
        }

        if (assemblyDirectory?.Exists != true || !playwrightDllExists)
        {
            VcrLogger.Logger.Debug("BaseDirectory doesn't contain Microsoft.Playwright.dll, using assembly location fallback");
            if (!string.IsNullOrEmpty(assembly.Location))
            {
                assemblyDirectory = new FileInfo(assembly.Location).Directory;
            }
            else
            {
                assemblyDirectory = new DirectoryInfo(baseDir);
            }
        }

        VcrLogger.Logger.Debug("Playwright would use assemblyDirectory: {AssemblyDirectory}", assemblyDirectory?.FullName ?? "(null)");

        // Log the three paths Playwright checks for drivers (in order)
        if (assemblyDirectory != null)
        {
            // Path 1: Direct path (for local builds)
            var directDriverPath = Path.Combine(assemblyDirectory.FullName, ".playwright", "node", platformId, nodeExecutable);
            var directDriverExists = File.Exists(directDriverPath);
            VcrLogger.Logger.Debug("Driver path 1 (direct): {Path} - Exists: {Exists}", directDriverPath, directDriverExists);

            // Path 2: NuGet package structure (assemblyDirectory/../../.playwright/)
            if (assemblyDirectory.Parent?.Parent != null)
            {
                var nugetDriverPath = Path.Combine(assemblyDirectory.Parent.Parent.FullName, ".playwright", "node", platformId, nodeExecutable);
                var nugetDriverExists = File.Exists(nugetDriverPath);
                VcrLogger.Logger.Debug("Driver path 2 (NuGet): {Path} - Exists: {Exists}", nugetDriverPath, nugetDriverExists);
            }
            else
            {
                VcrLogger.Logger.Debug("Driver path 2 (NuGet): Cannot check - assemblyDirectory has no grandparent");
            }

            // Also check for cli.js which is required
            var directCliPath = Path.Combine(assemblyDirectory.FullName, ".playwright", "package", "cli.js");
            var directCliExists = File.Exists(directCliPath);
            VcrLogger.Logger.Debug("CLI script (direct): {Path} - Exists: {Exists}", directCliPath, directCliExists);
        }

        // Check if playwright.ps1 wrapper exists (used by install command)
        var playwrightPs1Path = Path.Combine(baseDir, "playwright.ps1");
        var playwrightPs1Exists = File.Exists(playwrightPs1Path);
        VcrLogger.Logger.Debug("playwright.ps1 wrapper: {Path} - Exists: {Exists}", playwrightPs1Path, playwrightPs1Exists);

        VcrLogger.Logger.Debug("=== End Playwright Environment Diagnostic ===");
    }

    /// <summary>
    /// Checks if Playwright driver files are available and working.
    /// Attempts to create a Playwright instance to verify drivers are functional.
    /// </summary>
    /// <returns>True if drivers are available and working, false otherwise.</returns>
    private static async Task<bool> AreDriversAvailable()
    {
        VcrLogger.Logger.Debug("Checking if Playwright drivers are available...");

        // Log the environment to help diagnose issues
        LogPlaywrightEnvironment();

        try
        {
            VcrLogger.Logger.Debug("Attempting to create Playwright instance...");

            // Try to create a Playwright instance - this will fail if drivers are missing or incompatible
            // This is the most accurate test because it's exactly what we'll do later
            using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();

            VcrLogger.Logger.Debug("Successfully created Playwright instance - drivers are available");
            return true;
        }
        catch (PlaywrightException ex) when (ex.Message.Contains("Driver not found") ||
                                              ex.Message.Contains("missing required assets"))
        {
            // Drivers are missing or incompatible
            VcrLogger.Logger.Warning("Playwright drivers are not available: {Message}", ex.Message);
            VcrLogger.Logger.Debug("Full exception: {Exception}", ex.ToString());
            return false;
        }
        catch (Exception ex)
        {
            // Some other error - assume drivers are not working
            VcrLogger.Logger.Error(ex, "Unexpected error while checking Playwright drivers: {Message}", ex.Message);
            VcrLogger.Logger.Debug("Full exception: {Exception}", ex.ToString());
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