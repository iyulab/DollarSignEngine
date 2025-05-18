namespace DollarSignEngine.Internals;

/// <summary>
/// Base exception class for DollarSignEngine
/// </summary>
public class DollarSignEngineException : Exception
{
    /// <summary>
    /// Creates a new exception with a message
    /// </summary>
    public DollarSignEngineException(string message) : base(message)
    {
    }

    /// <summary>
    /// Creates a new exception with a message and inner exception
    /// </summary>
    public DollarSignEngineException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
