namespace DollarSignEngine;

/// <summary>
/// Delegate for resolving variable values
/// </summary>
public delegate object? ResolveVariableDelegate(string variableName);

/// <summary>
/// Delegate for handling errors in expression evaluation
/// </summary>
public delegate string? ErrorHandlerDelegate(string expression, Exception exception);

/// <summary>
/// Options for the DollarSign engine
/// </summary>
public class DollarSignOptions
{
    /// <summary>
    /// Whether to cache compiled expressions
    /// </summary>
    public bool UseCache { get; set; } = true;

    /// <summary>
    /// Whether to throw exceptions on errors
    /// </summary>
    public bool ThrowOnError { get; set; } = false;

    /// <summary>
    /// Custom variable resolver function
    /// </summary>
    public ResolveVariableDelegate? VariableResolver { get; set; }

    /// <summary>
    /// Custom error handler function
    /// </summary>
    public ErrorHandlerDelegate? ErrorHandler { get; set; }

    /// <summary>
    /// Gets default options
    /// </summary>
    public static DollarSignOptions Default => new();

    /// <summary>
    /// Creates options with a specific variable resolver
    /// </summary>
    public static DollarSignOptions WithResolver(ResolveVariableDelegate resolver) =>
        new() { VariableResolver = resolver };

    /// <summary>
    /// Creates options with a specific error handler
    /// </summary>
    public static DollarSignOptions WithErrorHandler(ErrorHandlerDelegate errorHandler) =>
        new() { ErrorHandler = errorHandler };

    /// <summary>
    /// Creates options that throw exceptions on errors
    /// </summary>
    public static DollarSignOptions Throwing() =>
        new() { ThrowOnError = true };
}