namespace DollarSignEngine.Utilities;

/// <summary>
/// Provides logging functionality for the DollarSign engine.
/// </summary>
internal static class Log
{
    /// <summary>
    /// Logs a debug message if debug logging is enabled.
    /// </summary>
    public static void Debug(string message, DollarSignOptions options)
    {
        if (options.EnableDebugLogging)
        {
            Console.WriteLine($"[DollarSignEngine] {message}");
        }
    }

    /// <summary>
    /// Logs a formatted debug message if debug logging is enabled.
    /// </summary>
    public static void Debug(string format, DollarSignOptions options, params object?[] args)
    {
        if (options.EnableDebugLogging)
        {
            Console.WriteLine($"[DollarSignEngine] {string.Format(format, args)}");
        }
    }

    /// <summary>
    /// Logs an error message.
    /// </summary>
    public static void Error(string message, DollarSignOptions options)
    {
        if (options.EnableDebugLogging)
        {
            Console.WriteLine($"[DollarSignEngine ERROR] {message}");
        }
    }

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    public static void Warning(string message, DollarSignOptions options)
    {
        if (options.EnableDebugLogging)
        {
            Console.WriteLine($"[DollarSignEngine WARNING] {message}");
        }
    }
}