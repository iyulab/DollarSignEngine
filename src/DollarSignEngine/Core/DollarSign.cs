using DollarSignEngine.Evaluation;
using DollarSignEngine.Parsing;

namespace DollarSignEngine;

/// <summary>
/// Provides functionality to dynamically evaluate C# expressions using interpolation strings and parameters.
/// </summary>
public static class DollarSign
{
    private static readonly ExpressionCache _expressionCache = new();
    private static readonly TemplateParser _templateParser = new();
    private static readonly FormatApplier _formatApplier = new();
    private static readonly ExpressionEvaluator _expressionEvaluator;

    static DollarSign()
    {
        _expressionEvaluator = new ExpressionEvaluator(_expressionCache);
    }

    /// <summary>
    /// Asynchronously evaluates a given C# interpolation string and returns the result.
    /// </summary>
    public static async Task<string> EvalAsync(string template, object? parameter = null, DollarSignOptions? options = null)
    {
        options ??= new DollarSignOptions();

        try
        {
            Log.Debug($"Processing template: {template}", options);

            // Parse the template and extract interpolated expressions
            var (processedTemplate, interpolationParts) = _templateParser.Parse(template, options);

            if (interpolationParts.Count == 0)
            {
                // No interpolated expressions found, return the original template with escaped braces handled
                return _templateParser.RestoreEscapedBraces(processedTemplate);
            }

            // Process each interpolation part
            return await ProcessInterpolationParts(processedTemplate, interpolationParts, parameter, options);
        }
        catch (Exception ex)
        {
            throw new DollarSignEngineException($"Error evaluating template: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Processes interpolation parts by evaluating and formatting them.
    /// </summary>
    private static async Task<string> ProcessInterpolationParts(string processedTemplate, List<InterpolationPart> parts, object? parameter, DollarSignOptions options)
    {
        foreach (var part in parts)
        {
            // Evaluate the expression
            object? result = await EvaluateExpressionPart(part.Expression, parameter, options);

            // Format the result
            var formattedResult = _formatApplier.Format(result, part.FormatSpecifier, part.Alignment, options.CultureInfo, options);
            Log.Debug($"Formatted result for '{part.Expression}': '{formattedResult}'", options);

            // Replace the placeholder with the formatted result
            processedTemplate = processedTemplate.Replace(part.Placeholder, formattedResult);
        }

        // Restore escaped braces
        return _templateParser.RestoreEscapedBraces(processedTemplate);
    }

    /// <summary>
    /// Evaluates a single expression part.
    /// </summary>
    private static async Task<object?> EvaluateExpressionPart(string expression, object? parameter, DollarSignOptions options)
    {
        try
        {
            Log.Debug($"Processing expression: {expression}", options);
            return await Task.Run(() => _expressionEvaluator.Evaluate(expression, parameter, options));
        }
        catch (Exception ex)
        {
            Log.Debug($"Error evaluating expression '{expression}': {ex.Message}", options);
            if (options.ThrowOnMissingParameter)
            {
                throw;
            }
            return null;
        }
    }

    /// <summary>
    /// Evaluates a single expression and returns its result.
    /// </summary>
    public static async Task<object?> EvaluateAsync(string expression, object? parameter = null, DollarSignOptions? options = null)
    {
        options ??= new DollarSignOptions();

        try
        {
            return await Task.Run(() => _expressionEvaluator.Evaluate(expression, parameter, options));
        }
        catch (Exception ex)
        {
            throw new DollarSignEngineException($"Error evaluating expression: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Clears the expression cache to free memory.
    /// </summary>
    public static void ClearCache()
    {
        _expressionCache.Clear();
    }
}