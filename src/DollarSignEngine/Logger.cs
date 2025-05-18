using System.Diagnostics;

namespace DollarSignEngine;

internal static class Logger
{
    [Conditional("DEBUG")]
    internal static void Debug(string message)
    {
        System.Diagnostics.Debug.WriteLine(message);
        Console.WriteLine(message);
    }
}
