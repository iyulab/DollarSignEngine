using System.Collections;
using System.Reflection;

namespace DollarSignEngine;

/// <summary>
/// Evaluates C# string interpolation expressions at runtime
/// </summary>
public static class DollarSign
{
    private static readonly DollarSignCompiler Compiler = new();
    private static readonly LinqExpressionEvaluator LinqEvaluator = new();

    /// <summary>
    /// Evaluates a string interpolation expression using an object's properties
    /// </summary>
    public static async Task<string> EvalAsync(
        string expression,
        object? variables = null,
        DollarSignOptions? options = null)
    {
        if (string.IsNullOrEmpty(expression))
            return string.Empty;

        var evalOptions = options ?? new DollarSignOptions();
        Logger.Debug($"[DollarSign.EvalAsync] Expression: {expression}");

        try
        {
            // Check for LINQ expressions that might require special handling
            if (ContainsLikeLyLinqExpression(expression))
            {
                Logger.Debug("[DollarSign.EvalAsync] Using LINQ evaluation path");
                // Use LINQ-specific evaluation path
                return await LinqEvaluator.EvaluateAsync(expression, variables, evalOptions);
            }

            Logger.Debug("[DollarSign.EvalAsync] Using standard compilation path");
            // Use standard compilation path for non-LINQ expressions
            // Create resolver from object or use options resolver
            var resolver = evalOptions.VariableResolver ??
                (name => variables == null ? null : ResolvePropertyValue(variables, name));

            var compiledExpression = await Compiler.CompileExpressionAsync(expression, evalOptions);
            return compiledExpression.Execute(resolver, evalOptions);
        }
        catch (CompilationException ex)
        {
            Logger.Debug($"[DollarSign.EvalAsync] CompilationException: {ex.Message}");
            if (evalOptions.ThrowOnError)
                throw;
            return string.Empty;
        }
        catch (DollarSignEngineException ex)
        {
            Logger.Debug($"[DollarSign.EvalAsync] DollarSignEngineException: {ex.Message}");
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
    /// Evaluates a string interpolation expression using a dictionary of variables
    /// </summary>
    public static async Task<string> EvalAsync(
        string expression,
        IDictionary<string, object?> variables,
        DollarSignOptions? options = null)
    {
        if (string.IsNullOrEmpty(expression))
            return string.Empty;

        var evalOptions = options ?? new DollarSignOptions();
        Logger.Debug($"[DollarSign.EvalAsync] Expression (dictionary): {expression}");

        try
        {
            // Check for LINQ expressions that might require special handling
            if (ContainsLikeLyLinqExpression(expression))
            {
                Logger.Debug("[DollarSign.EvalAsync] Using LINQ evaluation path (dictionary)");
                // Use LINQ-specific evaluation path with dictionary variables
                return await LinqEvaluator.EvaluateAsync(expression, variables, evalOptions);
            }

            Logger.Debug("[DollarSign.EvalAsync] Using standard compilation path (dictionary)");
            // Use standard compilation path for non-LINQ expressions
            // Create resolver from dictionary or use options resolver
            var resolver = evalOptions.VariableResolver ??
                (name => variables.TryGetValue(name, out var value) ? value : null);

            var compiledExpression = await Compiler.CompileExpressionAsync(expression, evalOptions);
            return compiledExpression.Execute(resolver, evalOptions);
        }
        catch (CompilationException ex)
        {
            Logger.Debug($"[DollarSign.EvalAsync] CompilationException (dictionary): {ex.Message}");
            if (evalOptions.ThrowOnError)
                throw;
            return string.Empty;
        }
        catch (DollarSignEngineException ex)
        {
            Logger.Debug($"[DollarSign.EvalAsync] DollarSignEngineException (dictionary): {ex.Message}");
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
    /// Synchronous version of EvalAsync using an object's properties
    /// </summary>
    public static string Eval(
        string expression,
        object? variables = null,
        DollarSignOptions? options = null)
    {
        return EvalAsync(expression, variables, options).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Synchronous version of EvalAsync using a dictionary of variables
    /// </summary>
    public static string Eval(
        string expression,
        IDictionary<string, object?> variables,
        DollarSignOptions? options = null)
    {
        return EvalAsync(expression, variables, options).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Clears the expression compilation cache
    /// </summary>
    public static void ClearCache()
    {
        Compiler.ClearCache();
    }

    /// <summary>
    /// Resolves a property value from an object
    /// </summary>
    private static object? ResolvePropertyValue(object source, string propertyPath)
    {
        if (source == null || string.IsNullOrEmpty(propertyPath))
            return null;

        try
        {
            // Handle nested properties (e.g., Address.City)
            if (propertyPath.Contains('.'))
            {
                string[] parts = propertyPath.Split('.');
                object? current = source;

                foreach (var part in parts)
                {
                    if (current == null)
                        return null;

                    current = ResolvePropertyValue(current, part);
                }

                return current;
            }

            // Handle dictionary
            if (source is IDictionary<string, object> dict && dict.TryGetValue(propertyPath, out var dictValue))
                return dictValue;

            // Handle standard IDictionary
            if (source is IDictionary nonGenericDict && nonGenericDict.Contains(propertyPath))
                return nonGenericDict[propertyPath];

            // Handle properties
            var property = source.GetType().GetProperty(propertyPath,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (property != null)
                return property.GetValue(source);

            return null;
        }
        catch
        {
            // Silently fail and return null
            return null;
        }
    }

    /// <summary>
    /// Checks if an expression is likely to contain LINQ methods requiring special handling
    /// </summary>
    private static bool ContainsLikeLyLinqExpression(string expression)
    {
        // Check for typical LINQ method calls
        var isLinq = expression.Contains(".Where(") ||
               expression.Contains(".Select(") ||
               expression.Contains(".OrderBy(") ||
               expression.Contains(".GroupBy(") ||
               expression.Contains(".Join(") ||
               expression.Contains(".Skip(") ||
               expression.Contains(".Take(") ||
               expression.Contains(".Any(") ||
               expression.Contains(".All(") ||
               expression.Contains(".First") ||
               expression.Contains(".Last") ||
               expression.Contains(".Single") ||
               expression.Contains(".Sum(") ||
               expression.Contains(".Average(") ||
               expression.Contains(".Min(") ||
               expression.Contains(".Max(") ||
               expression.Contains(".Count(");

        Logger.Debug($"[DollarSign] Expression '{expression}' contains LINQ: {isLinq}");
        return isLinq;
    }
}