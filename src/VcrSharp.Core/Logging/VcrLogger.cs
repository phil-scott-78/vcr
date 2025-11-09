using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace VcrSharp.Core.Logging;

/// <summary>
/// Centralized logger configuration for VcrSharp.
/// Provides file sink with configurable verbosity.
/// </summary>
public static class VcrLogger
{
    private static Logger? _logger;
    private static bool _isConfigured;

    /// <summary>
    /// Gets the current logger instance.
    /// </summary>
    public static ILogger Logger => _logger ?? throw new InvalidOperationException("Logger not configured. Call Configure() first.");

    /// <summary>
    /// Configures the global logger with the specified verbosity.
    /// </summary>
    /// <param name="verbose">If true, enables Debug-level logging. Otherwise, uses Information level.</param>
    public static void Configure(bool verbose = false)
    {
        if (_isConfigured)
        {
            return;
        }

        var logLevel = verbose ? LogEventLevel.Debug : LogEventLevel.Information;

        // Get current working directory for log files
        var logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "vcrsharp-logs");
        Directory.CreateDirectory(logDirectory);

        var logFilePath = Path.Combine(logDirectory, $"vcrsharp-{DateTime.Now:yyyyMMdd}.log");

        _logger = new LoggerConfiguration()
            .MinimumLevel.Is(logLevel)
            .WriteTo.File(
                logFilePath,
                restrictedToMinimumLevel: LogEventLevel.Debug, // Always log debug+ to file
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        _isConfigured = true;

        // Log the configuration
        if (verbose)
        {
            _logger.Debug("Verbose logging enabled. Log file: {LogFile}", logFilePath);
        }
        else
        {
            _logger.Information("Logging configured. Log file: {LogFile}", logFilePath);
        }
    }

    /// <summary>
    /// Closes and flushes the logger.
    /// </summary>
    public static void Close()
    {
        _logger?.Dispose();
        _logger = null;
        _isConfigured = false;
    }
}