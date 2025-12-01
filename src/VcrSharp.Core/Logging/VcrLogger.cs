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
    /// <param name="verbose">If true, enables file logging with Debug-level output. Otherwise, creates a silent logger.</param>
    public static void Configure(bool verbose = false)
    {
        if (_isConfigured)
        {
            return;
        }

        if (verbose)
        {
            // Get current working directory for log files
            var logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "vcrsharp-logs");
            Directory.CreateDirectory(logDirectory);

            var logFilePath = Path.Combine(logDirectory, $"vcrsharp-{DateTime.Now:yyyyMMdd}.log");

            _logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    logFilePath,
                    restrictedToMinimumLevel: LogEventLevel.Debug,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            _logger.Debug("Verbose logging enabled. Log file: {LogFile}", logFilePath);
        }
        else
        {
            // Create a silent logger (no sinks) when verbose is not enabled
            _logger = new LoggerConfiguration()
                .MinimumLevel.Fatal() // Effectively disables all logging
                .CreateLogger();
        }

        _isConfigured = true;
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