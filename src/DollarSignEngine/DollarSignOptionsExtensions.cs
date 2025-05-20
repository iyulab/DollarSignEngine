namespace DollarSignEngine;

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
}