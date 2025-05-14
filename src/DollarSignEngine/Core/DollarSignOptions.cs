using System.Globalization;

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
    /// Default is false.
    /// </summary>
    public bool EnableDebugLogging { get; set; }
#if DEBUG
        = true;
#else
        = false;
#endif

    /// <summary>
    /// Gets or sets additional assemblies to reference in the expression evaluation.
    /// </summary>
    public List<string> AdditionalNamespaces { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether to use strict mode for parameter access.
    /// In strict mode, accessing a non-existent parameter throws an exception instead of returning null.
    /// Default is false.
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
    /// Default is false.
    /// </summary>
    public bool SupportDollarSignSyntax { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to enable expression result caching.
    /// When true, evaluated expressions are cached to improve performance for repeated calls.
    /// Default is true.
    /// </summary>
    public bool EnableCaching { get; set; } = true;
}

/// <summary>
/// Delegate for resolving variable values from parameter objects.
/// </summary>
/// <param name="expression">The expression or variable name to resolve.</param>
/// <param name="parameter">The parameter object containing variable values.</param>
/// <returns>The resolved value or null if not resolved.</returns>
public delegate object? VariableResolverCallback(string expression, object? parameter);