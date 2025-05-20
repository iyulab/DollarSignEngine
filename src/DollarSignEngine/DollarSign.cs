namespace DollarSignEngine;

/// <summary>
/// Main entry point for the DollarSign template engine.
/// </summary>
public static class DollarSign
{
    private static readonly ExpressionEvaluator Evaluator = new();

    /// <summary>
    /// Empty context class for templates without parameters.
    /// </summary>
    public class NoParametersContext { }

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
    /// Clears all internal caches used by the engine.
    /// </summary>
    public static void ClearCache() => Evaluator.ClearCache();
}