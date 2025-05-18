using System.Diagnostics;

namespace DollarSignEngine.Internals;

/// <summary>
/// Internal logger for the DollarSignEngine.
/// </summary>
internal static class Logger
{
    /// <summary>
    /// Logs a debug message. Only compiled in DEBUG builds.
    /// </summary>
    [Conditional("DEBUG")]
    internal static void Debug(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[DollarSignEngine-Debug] {message}");
        Console.WriteLine($"[DollarSignEngine-Debug] {message}");
    }
}