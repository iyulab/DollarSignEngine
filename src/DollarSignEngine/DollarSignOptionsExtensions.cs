namespace DollarSignEngine;

/// <summary>
/// Extension methods for DollarSignOptions to provide fluent configuration.
/// </summary>
public static class DollarSignOptionsExtensions
{
    /// <summary>
    /// Sets a variable resolver on the existing options.
    /// </summary>
    public static DollarSignOptions WithResolver(this DollarSignOptions options, ResolveVariableDelegate resolver)
    {
        options.VariableResolver = resolver;
        return options;
    }

    /// <summary>
    /// Sets a specific error handler on the existing options.
    /// </summary>
    public static DollarSignOptions WithErrorHandler(this DollarSignOptions options, ErrorHandlerDelegate errorHandler)
    {
        options.ErrorHandler = errorHandler;
        return options;
    }

    /// <summary>
    /// Configures the existing options to throw exceptions on errors.
    /// </summary>
    public static DollarSignOptions Throwing(this DollarSignOptions options)
    {
        options.ThrowOnError = true;
        return options;
    }

    /// <summary>
    /// Enables dollar sign syntax support on the existing options.
    /// </summary>
    public static DollarSignOptions WithDollarSignSyntax(this DollarSignOptions options)
    {
        options.SupportDollarSignSyntax = true;
        return options;
    }

    /// <summary>
    /// Sets a specific culture for formatting on the existing options.
    /// </summary>
    public static DollarSignOptions WithCulture(this DollarSignOptions options, CultureInfo culture)
    {
        options.CultureInfo = culture;
        return options;
    }

    /// <summary>
    /// Adds global data to the DollarSignOptions that will be available in all templates.
    /// </summary>
    public static DollarSignOptions WithGlobalData(this DollarSignOptions options, object? globalData)
    {
        options.GlobalData = globalData;
        return options;
    }

    /// <summary>
    /// Sets the security level for expression evaluation.
    /// </summary>
    public static DollarSignOptions WithSecurity(this DollarSignOptions options, SecurityLevel level)
    {
        options.SecurityLevel = level;
        return options;
    }

    /// <summary>
    /// Configures strict security settings.
    /// </summary>
    public static DollarSignOptions WithStrictSecurity(this DollarSignOptions options)
    {
        options.SecurityLevel = SecurityLevel.Strict;
        options.ThrowOnError = true;
        options.TimeoutMs = Math.Min(options.TimeoutMs, 1000); // Max 1 second for strict mode
        return options;
    }

    /// <summary>
    /// Sets the execution timeout for expressions.
    /// </summary>
    public static DollarSignOptions WithTimeout(this DollarSignOptions options, TimeSpan timeout)
    {
        options.TimeoutMs = (int)timeout.TotalMilliseconds;
        return options;
    }

    /// <summary>
    /// Sets the execution timeout for expressions in milliseconds.
    /// </summary>
    public static DollarSignOptions WithTimeout(this DollarSignOptions options, int timeoutMs)
    {
        options.TimeoutMs = timeoutMs;
        return options;
    }

    /// <summary>
    /// Configures cache settings.
    /// </summary>
    public static DollarSignOptions WithCache(this DollarSignOptions options, int size, TimeSpan? ttl = null)
    {
        options.UseCache = true;
        options.CacheSize = size;
        if (ttl.HasValue)
            options.CacheTtl = ttl.Value;
        return options;
    }

    /// <summary>
    /// Disables caching for this configuration.
    /// </summary>
    public static DollarSignOptions WithoutCache(this DollarSignOptions options)
    {
        options.UseCache = false;
        return options;
    }

    /// <summary>
    /// Configures high-performance settings optimized for speed.
    /// </summary>
    public static DollarSignOptions OptimizedForPerformance(this DollarSignOptions options)
    {
        options.UseCache = true;
        options.CacheSize = 2000; // Larger cache
        options.CacheTtl = TimeSpan.FromHours(2); // Longer TTL
        options.ThrowOnError = false; // Don't throw to avoid exception overhead
        options.TreatUndefinedVariablesInSimpleExpressionsAsEmpty = true;
        return options;
    }

    /// <summary>
    /// Configures settings optimized for safety and security.
    /// </summary>
    public static DollarSignOptions OptimizedForSecurity(this DollarSignOptions options)
    {
        options.SecurityLevel = SecurityLevel.Strict;
        options.ThrowOnError = true;
        options.TimeoutMs = 1000; // Short timeout
        options.CacheSize = 500; // Smaller cache
        options.CacheTtl = TimeSpan.FromMinutes(30); // Shorter TTL
        return options;
    }

    /// <summary>
    /// Configures balanced settings for production use.
    /// </summary>
    public static DollarSignOptions OptimizedForProduction(this DollarSignOptions options)
    {
        options.SecurityLevel = SecurityLevel.Moderate;
        options.ThrowOnError = false; // Log errors but don't crash
        options.TimeoutMs = 3000; // Reasonable timeout
        options.UseCache = true;
        options.CacheSize = 1000;
        options.CacheTtl = TimeSpan.FromHours(1);
        options.TreatUndefinedVariablesInSimpleExpressionsAsEmpty = true;
        return options;
    }

    /// <summary>
    /// Creates a new instance with the specified configuration applied.
    /// </summary>
    public static DollarSignOptions Create(Action<DollarSignOptions> configure)
    {
        var options = new DollarSignOptions();
        configure(options);
        options.Validate();
        return options;
    }
}