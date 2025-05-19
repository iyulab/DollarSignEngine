namespace DollarSignEngine; // Or your target namespace for DollarSignEngineFeatures

/// <summary>
/// Base exception class for DollarSignEngine.
/// </summary>
public class DollarSignEngineException : Exception
{
    /// <summary>
    /// Creates a new exception with a message.
    /// </summary>
    public DollarSignEngineException(string message) : base(message)
    {
    }

    /// <summary>
    /// Creates a new exception with a message and inner exception.
    /// </summary>
    public DollarSignEngineException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when C# script compilation fails.
/// </summary>
public class CompilationException : DollarSignEngineException
{
    /// <summary>
    /// The details of the compilation error, typically from Roslyn diagnostics.
    /// </summary>
    public string ErrorDetails { get; }

    /// <summary>
    /// Creates a new compilation exception.
    /// </summary>
    public CompilationException(string message, string errorDetails)
        : base(message)
    {
        ErrorDetails = errorDetails;
    }

    /// <summary>
    /// Creates a new compilation exception with an inner exception.
    /// </summary>
    public CompilationException(string message, string errorDetails, Exception innerException)
        : base(message, innerException)
    {
        ErrorDetails = errorDetails;
    }
}