namespace DollarSignEngine.Internals;

/// <summary>
/// Log level enumeration.
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// Debug information, only logged in debug mode.
    /// </summary>
    Debug,

    /// <summary>
    /// Information messages.
    /// </summary>
    Info,

    /// <summary>
    /// Warning messages.
    /// </summary>
    Warning,

    /// <summary>
    /// Error messages.
    /// </summary>
    Error
}

/// <summary>
/// Internal logger for the DollarSignEngine.
/// </summary>
internal static class Logger
{
    // Current minimum log level
    private static LogLevel _minimumLevel = LogLevel.Warning;

    // Whether to include timestamps in log output
    private static bool _includeTimestamps = true;

    // Action to handle log messages
    private static Action<LogLevel, string>? _logHandler;

    /// <summary>
    /// Sets the minimum log level to display.
    /// </summary>
    /// <param name="level">The minimum log level.</param>
    public static void SetMinimumLevel(LogLevel level)
    {
        _minimumLevel = level;
    }

    /// <summary>
    /// Sets whether to include timestamps in log output.
    /// </summary>
    /// <param name="include">True to include timestamps, false to omit them.</param>
    public static void SetIncludeTimestamps(bool include)
    {
        _includeTimestamps = include;
    }

    /// <summary>
    /// Sets a custom log handler for all log output.
    /// </summary>
    /// <param name="handler">The handler action for log messages.</param>
    public static void SetLogHandler(Action<LogLevel, string>? handler)
    {
        _logHandler = handler;
    }

    /// <summary>
    /// Logs a debug message. Only visible when minimum level is Debug.
    /// </summary>
    internal static void Debug(string message)
    {
        Log(LogLevel.Debug, message);
    }

    /// <summary>
    /// Logs an info message.
    /// </summary>
    internal static void Info(string message)
    {
        Log(LogLevel.Info, message);
    }

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    internal static void Warning(string message)
    {
        Log(LogLevel.Warning, message);
    }

    /// <summary>
    /// Logs an error message.
    /// </summary>
    internal static void Error(string message)
    {
        Log(LogLevel.Error, message);
    }

    /// <summary>
    /// Core logging method.
    /// </summary>
    private static void Log(LogLevel level, string message)
    {
        if (level < _minimumLevel) return;

        if (_logHandler != null)
        {
            _logHandler(level, message);
            return;
        }

        string timestamp = _includeTimestamps ? $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} " : "";
        string logMessage = $"[DollarSignEngine-{level}] {timestamp}{message}";

        // Use different outputs based on level
        switch (level)
        {
            case LogLevel.Debug:
#if DEBUG
                System.Diagnostics.Debug.WriteLine(logMessage);
#endif
                break;

            case LogLevel.Info:
                Console.WriteLine(logMessage);
                break;

            case LogLevel.Warning:
                Console.WriteLine(logMessage);
                break;

            case LogLevel.Error:
                Console.Error.WriteLine(logMessage);
                break;
        }
    }
}