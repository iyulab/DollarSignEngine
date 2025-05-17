namespace DollarSignEngine;

/// <summary>
/// Delegate for resolving variable values
/// </summary>
public delegate object? ResolveVariableDelegate(string variableName);

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
    /// Gets default options
    /// </summary>
    public static DollarSignOptions Default => new();

    /// <summary>
    /// Creates options with a specific variable resolver
    /// </summary>
    public static DollarSignOptions WithResolver(ResolveVariableDelegate resolver) =>
        new() { VariableResolver = resolver };
}
