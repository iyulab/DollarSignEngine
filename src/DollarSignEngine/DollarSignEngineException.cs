namespace DollarSignEngine;

/// <summary>
/// Exception thrown when an error occurs during DollarSign expression evaluation.
/// </summary>
public class DollarSignEngineException : Exception
{
    /// <summary>
    /// Initializes a new instance of the DollarSignEngineException class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public DollarSignEngineException(string? message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the DollarSignEngineException class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public DollarSignEngineException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}