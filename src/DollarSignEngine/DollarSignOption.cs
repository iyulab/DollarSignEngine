using System.Globalization;

namespace DollarSignEngine;

/// <summary>
/// Delegate for resolving variable values from parameter objects.
/// </summary>
/// <param name="expression">The expression or variable name to resolve.</param>
/// <param name="parameter">The parameter object containing variable values.</param>
/// <returns>The resolved value or null if not resolved.</returns>
public delegate object? VariableResolverCallback(string expression, object? parameter);

/// <summary>
/// Options for configuring the behavior of the DollarSign engine.
/// </summary>
public class DollarSignOption
{
    /// <summary>
    /// Gets or sets a value indicating whether to throw an exception when a parameter is missing.
    /// </summary>
    public bool ThrowOnMissingParameter { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to enable debug logging.
    /// </summary>
    public bool EnableDebugLogging { get; set; } = false;

    /// <summary>
    /// Gets or sets additional namespaces to import in the script.
    /// </summary>
    public List<string> AdditionalNamespaces { get; set; } = new();

    /// <summary>
    /// Gets or sets additional assemblies to reference in the script.
    /// </summary>
    public List<string> AdditionalAssemblies { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether to use strict mode for parameter access.
    /// In strict mode, accessing a non-existent parameter throws an exception instead of returning null.
    /// </summary>
    public bool StrictParameterAccess { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to include helper methods in the script.
    /// </summary>
    public bool IncludeHelperMethods { get; set; } = true;

    /// <summary>
    /// Gets or sets the culture to use for formatting operations.
    /// If null, the current culture is used.
    /// </summary>
    public CultureInfo? FormattingCulture { get; set; } = null;

    /// <summary>
    /// Gets or sets a callback for custom variable resolution before falling back to script evaluation.
    /// Return null from the callback to indicate the variable could not be resolved and should be processed normally.
    /// </summary>
    public VariableResolverCallback? VariableResolver { get; set; } = null;

    /// <summary>
    /// Gets or sets a value indicating whether to prefer callback-based resolution over script evaluation.
    /// When true, script evaluation will only be used when the VariableResolver returns null.
    /// </summary>
    public bool PreferCallbackResolution { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to support dollar sign prefixed variables (${name}).
    /// When true, expressions like "${name}" will be evaluated and "{name}" will be left as-is.
    /// When false (default), expressions like "{name}" will be evaluated and "${name}" will be left as-is.
    /// </summary>
    public bool SupportDollarSignSyntax { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to directly evaluate array and collection indexing 
    /// before falling back to script evaluation. Enabling this can significantly improve performance
    /// for common indexing operations like "array[0]" or "dictionary["key"]".
    /// </summary>
    public bool OptimizeCollectionAccess { get; set; } = true;
}