namespace DollarSignEngine;

/// <summary>
/// Options for configuring the behavior of the DollarSign engine.
/// </summary>
public class DollarSignOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to throw an exception when a parameter is missing.
    /// Default is false.
    /// </summary>
    public bool ThrowOnMissingParameter { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to enable debug logging.
    /// </summary>
    public bool EnableDebugLogging { get; set; }
#if DEBUG
        = true;
#else
        = false;
#endif

    /// <summary>
    /// Gets or sets additional namespaces to import in the expression evaluation.
    /// </summary>
    public List<string> AdditionalNamespaces { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether to use strict mode for parameter access.
    /// In strict mode, accessing a non-existent parameter throws an exception instead of returning null.
    /// </summary>
    public bool StrictParameterAccess { get; set; } = false;

    /// <summary>
    /// Gets or sets the culture to use for formatting operations.
    /// If null, the current culture is used.
    /// </summary>
    public CultureInfo? CultureInfo { get; set; } = null;

    /// <summary>
    /// Gets or sets a callback for custom variable resolution before falling back to expression evaluation.
    /// Return null from the callback to indicate the variable could not be resolved and should be processed normally.
    /// </summary>
    public VariableResolverCallback? VariableResolver { get; set; } = null;

    /// <summary>
    /// Gets or sets a value indicating whether to support dollar sign prefixed variables (${name}).
    /// When true, expressions like "${name}" will be evaluated and "{name}" will be left as-is.
    /// When false (default), expressions like "{name}" will be evaluated and "${name}" will be left as-is.
    /// </summary>
    public bool SupportDollarSignSyntax { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to enable expression result caching.
    /// When true, evaluated expressions are cached to improve performance for repeated calls.
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to optimize ternary operator evaluation.
    /// When true, only the relevant branch of a ternary expression is evaluated based on the condition.
    /// </summary>
    public bool OptimizeTernaryEvaluation { get; set; } = true;
}

/// <summary>
/// Delegate for resolving variable values from parameter objects.
/// </summary>
public delegate object? VariableResolverCallback(string expression, object? parameter);