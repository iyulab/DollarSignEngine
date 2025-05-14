using DollarSignEngine.Evaluation;
using DollarSignEngine.Formatting;
using DollarSignEngine.Parsing;

namespace DollarSignEngine;

/// <summary>
/// Provides functionality to dynamically evaluate C# expressions using interpolation strings and parameters.
/// </summary>
public static class DollarSign
{
    private static readonly ExpressionCache _expressionCache = new();
    private static readonly TemplateParser _templateParser = new();
    private static readonly FormatSpecifierParser _formatParser = new();
    private static readonly FormatApplier _formatApplier = new();
    private static readonly ExpressionEvaluator _expressionEvaluator;

    static DollarSign()
    {
        _expressionEvaluator = new ExpressionEvaluator(_expressionCache);
    }

    /// <summary>
    /// Asynchronously evaluates a given C# expression as a string and returns the result.
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
            foreach (var part in interpolationParts)
            {
                // Extract format specifier and alignment
                var (expression, alignment, formatSpecifier) = _formatParser.Parse(part.Expression);
                part.Alignment = alignment;
                part.FormatSpecifier = formatSpecifier;

                // Evaluate the expression
                var result = await Task.Run(() => _expressionEvaluator.Evaluate(expression, parameter, options));

                // Format the result
                var formattedResult = _formatApplier.Format(result, part.FormatSpecifier, part.Alignment, options.CultureInfo, options);

                // Replace the placeholder with the formatted result
                processedTemplate = processedTemplate.Replace(part.Placeholder, formattedResult);
            }

            // Restore escaped braces
            return _templateParser.RestoreEscapedBraces(processedTemplate);
        }
        catch (Exception ex)
        {
            throw new DollarSignEngineException($"Error evaluating template: {ex.Message}", ex);
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