namespace DollarSignEngine;

/// <summary>
/// Exception thrown when compilation fails
/// </summary>
public class CompilationException : DollarSignEngineException
{
    /// <summary>
    /// The source code that failed to compile
    /// </summary>
    public string SourceCode { get; }

    /// <summary>
    /// Creates a new compilation exception
    /// </summary>
    public CompilationException(string message, string sourceCode)
        : base(message)
    {
        SourceCode = sourceCode;
    }
}
