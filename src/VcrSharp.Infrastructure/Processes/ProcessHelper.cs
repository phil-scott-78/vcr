using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VcrSharp.Infrastructure.Processes;

/// <summary>
/// Helper utilities for process management.
/// </summary>
public static class ProcessHelper
{
    /// <summary>
    /// Checks if a program is available in the system PATH.
    /// </summary>
    /// <param name="programName">The program name to check.</param>
    /// <returns>True if the program is found, false otherwise.</returns>
    public static bool IsProgramAvailable(string programName)
    {
        try
        {
            var whichCommand = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which";
            var startInfo = new ProcessStartInfo
            {
                FileName = whichCommand,
                Arguments = programName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                return false;

            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Kills a process and all its children.
    /// </summary>
    /// <param name="process">The process to kill.</param>
    public static void KillProcessTree(Process process)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Use taskkill on Windows to kill the process tree
                Process.Start(new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = $"/PID {process.Id} /T /F",
                    CreateNoWindow = true,
                    UseShellExecute = false
                })?.WaitForExit();
            }
            else
            {
                // On Unix-like systems, kill the process group
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best effort - ignore errors
        }
    }
}