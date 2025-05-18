using System.Collections;

namespace DollarSignEngine;

/// <summary>
/// Main API for evaluating C# string interpolation expressions at runtime.
/// </summary>
public static class DollarSign
{
    private static readonly ExpressionEvaluator Evaluator = new();

    /// <summary>
    /// Evaluates a string interpolation expression using an object's properties.
    /// </summary>
    public static async Task<string> EvalAsync(
        string expression,
        object? variables = null,
        DollarSignOptions? options = null)
    {
        if (string.IsNullOrEmpty(expression))
            return string.Empty;

        var evalOptions = options?.Clone() ?? new DollarSignOptions();
        Logger.Debug($"[DollarSign.EvalAsync] Expression: {expression}");

        try
        {
            if (variables != null)
            {
                evalOptions.VariableResolver = name => ResolvePropertyValue(variables, name);
            }

            return await Evaluator.EvaluateAsync(expression, variables, evalOptions);
        }
        catch (DollarSignEngineException)
        {
            if (evalOptions.ThrowOnError)
                throw;
            return string.Empty;
        }
        catch (Exception ex)
        {
            Logger.Debug($"[DollarSign.EvalAsync] Exception: {ex.GetType().Name}: {ex.Message}");
            if (evalOptions.ThrowOnError)
                throw new DollarSignEngineException("Error evaluating expression", ex);
            return string.Empty;
        }
    }

    /// <summary>
    /// Evaluates a string interpolation expression using a dictionary of variables.
    /// </summary>
    public static async Task<string> EvalAsync(
        string expression,
        IDictionary<string, object?> variables,
        DollarSignOptions? options = null)
    {
        if (string.IsNullOrEmpty(expression))
            return string.Empty;

        if (variables is null)
            return await EvalAsync(expression, (object?)null, options);

        var evalOptions = options?.Clone() ?? new DollarSignOptions();
        Logger.Debug($"[DollarSign.EvalAsync] Expression (dictionary): {expression}");

        try
        {
            evalOptions.VariableResolver = name => variables.TryGetValue(name, out var v) ? v : null;
            var wrapper = new DictionaryWrapper(variables);
            return await Evaluator.EvaluateAsync(expression, wrapper, evalOptions);
        }
        catch (DollarSignEngineException)
        {
            if (evalOptions.ThrowOnError)
                throw;
            return string.Empty;
        }
        catch (Exception ex)
        {
            Logger.Debug($"[DollarSign.EvalAsync] Exception (dictionary): {ex.GetType().Name}: {ex.Message}");
            if (evalOptions.ThrowOnError)
                throw new DollarSignEngineException("Error evaluating expression", ex);
            return string.Empty;
        }
    }

    /// <summary>
    /// Synchronous version using object variables.
    /// </summary>
    public static string Eval(
        string expression,
        object? variables = null,
        DollarSignOptions? options = null)
        => EvalAsync(expression, variables, options).GetAwaiter().GetResult();

    /// <summary>
    /// Synchronous version using dictionary variables.
    /// </summary>
    public static string Eval(
        string expression,
        IDictionary<string, object?> variables,
        DollarSignOptions? options = null)
        => EvalAsync(expression, variables, options).GetAwaiter().GetResult();

    /// <summary>
    /// Clears the expression compilation cache.
    /// </summary>
    public static void ClearCache() => Evaluator.ClearCache();

    /// <summary>
    /// Resolves a property value from an object. Handles nested properties and dictionaries.
    /// </summary>
    private static object? ResolvePropertyValue(object source, string propertyPath)
    {
        if (source == null || string.IsNullOrEmpty(propertyPath))
            return null;

        try
        {
            object? current = source;
            string[] parts = propertyPath.Split('.');

            foreach (var part in parts)
            {
                if (current == null)
                    return null;

                if (current is DictionaryWrapper dw)
                {
                    current = dw.TryGetValue(part);
                    continue;
                }

                if (current is IDictionary<string, object> genericDict && genericDict.TryGetValue(part, out var dictValue))
                {
                    current = dictValue;
                    continue;
                }

                if (current is IDictionary nonGenericDict && nonGenericDict.Contains(part))
                {
                    current = nonGenericDict[part];
                    continue;
                }

                var property = current.GetType().GetProperty(part,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                if (property != null)
                {
                    current = property.GetValue(current);
                }
                else
                {
                    return null;
                }
            }
            return current;
        }
        catch
        {
            return null;
        }
    }
}