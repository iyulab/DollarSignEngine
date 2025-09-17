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

/// <summary>
/// Exception thrown when expression validation fails due to security concerns.
/// </summary>
public class ExpressionValidationException : DollarSignEngineException
{
    /// <summary>
    /// The expression that failed validation.
    /// </summary>
    public string Expression { get; }

    /// <summary>
    /// Suggested fix or alternative approach.
    /// </summary>
    public string? Suggestion { get; }

    /// <summary>
    /// The security level that was applied during validation.
    /// </summary>
    public SecurityLevel SecurityLevel { get; }

    /// <summary>
    /// Creates a new expression validation exception.
    /// </summary>
    public ExpressionValidationException(string expression, SecurityLevel securityLevel, string message, string? suggestion = null)
        : base(message)
    {
        Expression = expression;
        SecurityLevel = securityLevel;
        Suggestion = suggestion;
    }

    /// <summary>
    /// Creates a new expression validation exception with an inner exception.
    /// </summary>
    public ExpressionValidationException(string expression, SecurityLevel securityLevel, string message, string? suggestion, Exception innerException)
        : base(message, innerException)
    {
        Expression = expression;
        SecurityLevel = securityLevel;
        Suggestion = suggestion;
    }
}

/// <summary>
/// Exception thrown when expression execution times out.
/// </summary>
public class ExpressionTimeoutException : DollarSignEngineException
{
    /// <summary>
    /// The expression that timed out.
    /// </summary>
    public string Expression { get; }

    /// <summary>
    /// The timeout that was configured.
    /// </summary>
    public TimeSpan Timeout { get; }

    /// <summary>
    /// Creates a new expression timeout exception.
    /// </summary>
    public ExpressionTimeoutException(string expression, TimeSpan timeout)
        : base($"Expression execution timed out after {timeout.TotalMilliseconds}ms: {expression}")
    {
        Expression = expression;
        Timeout = timeout;
    }

    /// <summary>
    /// Creates a new expression timeout exception with an inner exception.
    /// </summary>
    public ExpressionTimeoutException(string expression, TimeSpan timeout, Exception innerException)
        : base($"Expression execution timed out after {timeout.TotalMilliseconds}ms: {expression}", innerException)
    {
        Expression = expression;
        Timeout = timeout;
    }
}

/// <summary>
/// Exception thrown when variable resolution fails.
/// </summary>
public class VariableResolutionException : DollarSignEngineException
{
    /// <summary>
    /// The variable name that could not be resolved.
    /// </summary>
    public string VariableName { get; }

    /// <summary>
    /// Available variable names in the context.
    /// </summary>
    public IReadOnlyList<string> AvailableVariables { get; }

    /// <summary>
    /// Creates a new variable resolution exception.
    /// </summary>
    public VariableResolutionException(string variableName, IEnumerable<string> availableVariables)
        : base(CreateMessage(variableName, availableVariables))
    {
        VariableName = variableName;
        AvailableVariables = availableVariables.ToList().AsReadOnly();
    }

    private static string CreateMessage(string variableName, IEnumerable<string> availableVariables)
    {
        var available = availableVariables.ToList();
        var message = $"Variable '{variableName}' could not be resolved.";

        if (available.Count > 0)
        {
            message += $" Available variables: {string.Join(", ", available)}";

            // Suggest similar variable names
            var similar = available.Where(v =>
                v.Contains(variableName, StringComparison.OrdinalIgnoreCase) ||
                variableName.Contains(v, StringComparison.OrdinalIgnoreCase))
                .Take(3)
                .ToList();

            if (similar.Count > 0)
            {
                message += $". Did you mean: {string.Join(", ", similar)}?";
            }
        }

        return message;
    }
}