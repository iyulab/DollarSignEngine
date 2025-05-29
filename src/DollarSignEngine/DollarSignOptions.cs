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
/// Options for the DollarSign engine with enhanced security and performance settings.
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
    /// Security level for expression validation. Defaults to Permissive.
    /// </summary>
    public SecurityLevel SecurityLevel { get; set; } = SecurityLevel.Permissive;

    /// <summary>
    /// Maximum execution time for expressions in milliseconds. Defaults to 5000ms (5 seconds).
    /// </summary>
    public int TimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Maximum cache size for compiled expressions. Defaults to 1000.
    /// </summary>
    public int CacheSize { get; set; } = 1000;

    /// <summary>
    /// Time-to-live for cached expressions. Defaults to 1 hour.
    /// </summary>
    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Global data object that will be available to all templates.
    /// Set through the WithGlobalData extension method.
    /// </summary>
    internal object? GlobalData { get; set; }

    /// <summary>
    /// Gets default options.
    /// </summary>
    public static DollarSignOptions Default => new();

    /// <summary>
    /// Creates options configured for strict security.
    /// </summary>
    public static DollarSignOptions Strict => new()
    {
        SecurityLevel = SecurityLevel.Strict,
        ThrowOnError = true,
        TimeoutMs = 1000,
        CacheSize = 500
    };

    /// <summary>
    /// Creates options configured for moderate security.
    /// </summary>
    public static DollarSignOptions Moderate => new()
    {
        SecurityLevel = SecurityLevel.Moderate,
        TimeoutMs = 3000,
        CacheSize = 750
    };

    /// <summary>
    /// Validates options for consistency and security.
    /// </summary>
    public void Validate()
    {
        if (CultureInfo == null)
            throw new ArgumentNullException(nameof(CultureInfo));

        if (TimeoutMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(TimeoutMs), "Timeout must be greater than zero.");

        if (CacheSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(CacheSize), "Cache size must be greater than zero.");

        if (CacheTtl <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(CacheTtl), "Cache TTL must be greater than zero.");
    }

    /// <summary>
    /// Creates deep clone of options.
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
        SecurityLevel = this.SecurityLevel,
        TimeoutMs = this.TimeoutMs,
        CacheSize = this.CacheSize,
        CacheTtl = this.CacheTtl,
        GlobalData = this.GlobalData
    };
}