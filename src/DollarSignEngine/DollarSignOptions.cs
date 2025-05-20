namespace DollarSignEngine;

/// <summary>
/// Delegate for resolving variable values.
/// </summary>
public delegate object? ResolveVariableDelegate(string variableName);
/// <summary>
/// Delegate for handling errors in expression evaluation.
/// </summary>
public delegate string? ErrorHandlerDelegate(string expression, Exception exception);

/// <summary>
/// Options for the DollarSign engine.
/// </summary>
public class DollarSignOptions
{
    /// <summary>
    /// Whether to cache compiled expressions. Defaults to true.
    /// </summary>
    public bool UseCache { get; set; } = true;

    /// <summary>
    /// Whether to throw exceptions on errors during evaluation. Defaults to false.
    /// </summary>
    public bool ThrowOnError { get; set; } = false;

    /// <summary>
    /// Custom variable resolver function.
    /// </summary>
    public ResolveVariableDelegate? VariableResolver { get; set; }

    /// <summary>
    /// Custom error handler function.
    /// </summary>
    public ErrorHandlerDelegate? ErrorHandler { get; set; }

    /// <summary>
    /// The culture to use for formatting. If null, the current culture is used.
    /// </summary>
    public CultureInfo CultureInfo { get; set; } = CultureInfo.CurrentCulture;

    /// <summary>
    /// Whether to support dollar sign syntax in templates.
    /// When enabled, {expression} is treated as literal text and ${expression} is evaluated.
    /// Defaults to false.
    /// </summary>
    public bool SupportDollarSignSyntax { get; set; } = false;

    /// <summary>
    /// Whether to treat undefined simple variables in expressions (e.g. "{UndefinedVar}")
    /// as empty strings instead of throwing an error. This applies when no custom ErrorHandler is specified
    /// and ThrowOnError is false. Defaults to true.
    /// </summary>
    public bool TreatUndefinedVariablesInSimpleExpressionsAsEmpty { get; set; } = true;

    /// <summary>
    /// Global data object that will be available to all templates.
    /// Set through the WithGlobalData extension method.
    /// </summary>
    internal object? GlobalData { get; set; }

    /// <summary>
    /// Internal use: Carries type information for the CastingDictionaryAccessRewriter.
    /// </summary>
    internal IDictionary<string, Type>? GlobalVariableTypes { get; set; }


    /// <summary>
    /// Gets default options.
    /// </summary>
    public static DollarSignOptions Default => new();

    /// <summary>
    /// Creates a shallow clone of the options.
    /// </summary>
    public DollarSignOptions Clone() => new()
    {
        UseCache = this.UseCache,
        ThrowOnError = this.ThrowOnError,
        VariableResolver = this.VariableResolver,
        ErrorHandler = this.ErrorHandler,
        CultureInfo = this.CultureInfo,
        SupportDollarSignSyntax = this.SupportDollarSignSyntax,
        TreatUndefinedVariablesInSimpleExpressionsAsEmpty = this.TreatUndefinedVariablesInSimpleExpressionsAsEmpty,
        GlobalVariableTypes = this.GlobalVariableTypes, // Shallow copy is fine for the dictionary reference
        GlobalData = this.GlobalData // Store the global data in the clone
    };
}