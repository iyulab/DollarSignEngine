namespace DollarSignEngine;

/// <summary>
/// Main entry point for the DollarSign template engine with enhanced performance and security.
/// </summary>
public static class DollarSign
{
    private static readonly Lazy<ExpressionEvaluator> LazyEvaluator =
        new(() => new ExpressionEvaluator(), LazyThreadSafetyMode.ExecutionAndPublication);

    private static ExpressionEvaluator Evaluator => LazyEvaluator.Value;

    /// <summary>
    /// Empty context class for templates without parameters.
    /// </summary>
    public class NoParametersContext { }

    /// <summary>
    /// Gets performance metrics from the expression evaluator.
    /// </summary>
    public static (long TotalEvaluations, long CacheHits, double HitRate) GetMetrics()
    {
        return Evaluator.GetMetrics();
    }

    /// <summary>
    /// Evaluates a template string asynchronously, replacing expressions with their computed values.
    /// </summary>
    /// <param name="expression">The template string containing expressions to evaluate.</param>
    /// <param name="variables">Optional context object with variables for the template.</param>
    /// <param name="options">Optional customization options for the evaluation.</param>
    /// <returns>The evaluated template with expressions replaced by their values.</returns>
    public static async Task<string> EvalAsync(
        string expression,
        object? variables = null,
        DollarSignOptions? options = null)
    {
        if (string.IsNullOrEmpty(expression))
            return string.Empty;

        var effectiveOptions = options?.Clone() ?? DollarSignOptions.Default;

        // Validate options for security and consistency
        effectiveOptions.Validate();

        Logger.Debug($"[DollarSign.EvalAsync] Expression: {expression}");

        // Prepare context for evaluator
        object contextForEvaluator = DataPreparationHelper.PrepareContext(variables, effectiveOptions);

        try
        {
            string result = await Evaluator.EvaluateAsync(expression, contextForEvaluator, effectiveOptions);
            return result;
        }
        catch (DollarSignEngineException)
        {
            if (effectiveOptions.ThrowOnError) throw;
            return string.Empty;
        }
        catch (Exception ex)
        {
            Logger.Debug($"[DollarSign.EvalAsync] General Exception: {ex.GetType().Name}: {ex.Message}");
            if (effectiveOptions.ThrowOnError)
                throw new DollarSignEngineException($"Error evaluating expression: \"{expression}\"", ex);
            return string.Empty;
        }
    }

    /// <summary>
    /// Evaluates a template string synchronously, replacing expressions with their computed values.
    /// </summary>
    /// <param name="expression">The template string containing expressions to evaluate.</param>
    /// <param name="variables">Optional context object with variables for the template.</param>
    /// <param name="options">Optional customization options for the evaluation.</param>
    /// <returns>The evaluated template with expressions replaced by their values.</returns>
    public static string Eval(
        string expression,
        object? variables = null,
        DollarSignOptions? options = null)
        => EvalAsync(expression, variables, options).GetAwaiter().GetResult();

    /// <summary>
    /// Evaluates a template with strong typing for the result.
    /// </summary>
    /// <typeparam name="T">The expected return type.</typeparam>
    /// <param name="expression">The template string containing expressions to evaluate.</param>
    /// <param name="variables">Optional context object with variables for the template.</param>
    /// <param name="options">Optional customization options for the evaluation.</param>
    /// <returns>The evaluated result cast to the specified type.</returns>
    public static async Task<T> EvalAsync<T>(
        string expression,
        object? variables = null,
        DollarSignOptions? options = null)
    {
        var result = await EvalAsync(expression, variables, options);

        if (result is T typedResult)
            return typedResult;

        // Attempt type conversion
        try
        {
            if (typeof(T) == typeof(string))
                return (T)(object)result;

            return (T)Convert.ChangeType(result, typeof(T));
        }
        catch (Exception ex)
        {
            throw new DollarSignEngineException(
                $"Cannot convert result '{result}' to type {typeof(T).Name}", ex);
        }
    }

    /// <summary>
    /// Evaluates a template with strong typing synchronously.
    /// </summary>
    public static T Eval<T>(
        string expression,
        object? variables = null,
        DollarSignOptions? options = null)
        => EvalAsync<T>(expression, variables, options).GetAwaiter().GetResult();

    /// <summary>
    /// Evaluates multiple templates in parallel for better performance.
    /// </summary>
    /// <param name="templates">Dictionary of template names and expressions.</param>
    /// <param name="variables">Optional context object with variables for the templates.</param>
    /// <param name="options">Optional customization options for the evaluation.</param>
    /// <returns>Dictionary of template names and their evaluated results.</returns>
    public static async Task<Dictionary<string, string>> EvalManyAsync(
        Dictionary<string, string> templates,
        object? variables = null,
        DollarSignOptions? options = null)
    {
        if (templates == null || templates.Count == 0)
            return new Dictionary<string, string>();

        var tasks = templates.Select(async kvp =>
        {
            try
            {
                var result = await EvalAsync(kvp.Value, variables, options);
                return new KeyValuePair<string, string>(kvp.Key, result);
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error evaluating template '{kvp.Key}': {ex.Message}");
                return new KeyValuePair<string, string>(kvp.Key,
                    options?.ThrowOnError == true ? throw ex : string.Empty);
            }
        });

        var results = await Task.WhenAll(tasks);
        return results.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    /// Validates if an expression is safe without executing it.
    /// </summary>
    /// <param name="expression">The expression to validate.</param>
    /// <param name="securityLevel">The security level to apply.</param>
    /// <returns>True if the expression is considered safe.</returns>
    public static bool ValidateExpression(string expression, SecurityLevel securityLevel = SecurityLevel.Permissive)
    {
        return SecurityValidator.IsSafeExpression(expression, securityLevel);
    }

    /// <summary>
    /// Clears all internal caches used by the engine.
    /// </summary>
    public static void ClearCache()
    {
        if (LazyEvaluator.IsValueCreated)
        {
            Evaluator.ClearCache();
        }
    }

    /// <summary>
    /// Forces cleanup of resources. Use this when shutting down the application.
    /// </summary>
    public static void Cleanup()
    {
        if (LazyEvaluator.IsValueCreated)
        {
            Evaluator.Dispose();
        }

        AssemblyReferenceHelper.ClearCache();
        TypeAccessorFactory.ClearCache();
        TypeNameHelper.ClearCaches();
    }
}