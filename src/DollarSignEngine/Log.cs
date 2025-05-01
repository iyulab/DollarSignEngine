namespace DollarSignEngine;

internal static class Log
{
    public static void Debug(string message, DollarSignOption option)
    {
        if (option.EnableDebugLogging)
        {
            Console.WriteLine(message);
        }
    }

    public static void Debug(string format, DollarSignOption option, params object?[] args)
    {
        if (option.EnableDebugLogging)
        {
            Console.WriteLine(format, args);
        }
    }
}