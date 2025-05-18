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
    public CultureInfo? CultureInfo { get; set; }

    /// <summary>
    /// Whether to support dollar sign syntax in templates. 
    /// When enabled, {expression} is treated as literal text and ${expression} is evaluated. 
    /// Defaults to false.
    /// </summary>
    public bool SupportDollarSignSyntax { get; set; } = false;

    /// <summary>
    /// Gets default options.
    /// </summary>
    public static DollarSignOptions Default => new();

    /// <summary>
    /// Creates options with a specific variable resolver.
    /// </summary>
    public static DollarSignOptions WithResolver(ResolveVariableDelegate resolver) =>
        new() { VariableResolver = resolver };

    /// <summary>
    /// Creates options with a specific error handler.
    /// </summary>
    public static DollarSignOptions WithErrorHandler(ErrorHandlerDelegate errorHandler) =>
        new() { ErrorHandler = errorHandler };

    /// <summary>
    /// Creates options that throw exceptions on errors.
    /// </summary>
    public static DollarSignOptions Throwing() =>
        new() { ThrowOnError = true };

    /// <summary>
    /// Creates options with dollar sign syntax support enabled.
    /// When enabled, {expression} is treated as literal text and ${expression} is evaluated.
    /// </summary>
    public static DollarSignOptions WithDollarSignSyntax() =>
        new() { SupportDollarSignSyntax = true };

    /// <summary>
    /// Creates options with a specific culture for formatting.
    /// </summary>
    /// <param name="culture">The culture to use for formatting expressions.</param>
    /// <returns>A new options instance with the specified culture.</returns>
    public static DollarSignOptions WithCulture(CultureInfo culture) =>
        new() { CultureInfo = culture };

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
        SupportDollarSignSyntax = this.SupportDollarSignSyntax
    };
}