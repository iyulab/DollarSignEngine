namespace DollarSignEngine.Utilities;

/// <summary>
/// Provides logging functionality for the DollarSign engine.
/// </summary>
internal static class Log
{
    /// <summary>
    /// Logs a debug message if debug logging is enabled.
    /// </summary>
    public static void Debug(string message, DollarSignOptions option)
    {
        if (option.EnableDebugLogging)
        {
            Console.WriteLine(message);
        }
    }

    /// <summary>
    /// Logs a formatted debug message if debug logging is enabled.
    /// </summary>
    public static void Debug(string format, DollarSignOptions option, params object?[] args)
    {
        if (option.EnableDebugLogging)
        {
            Console.WriteLine(format, args);
        }
    }
}