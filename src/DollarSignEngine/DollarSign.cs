using System.Globalization;
using System.Text.RegularExpressions;

namespace DollarSignEngine;

/// <summary>
/// Provides functionality to dynamically evaluate C# expressions using interpolation strings and parameters.
/// </summary>
public static partial class DollarSign
{
    // Unique mask pattern (very unlikely to appear in real strings)
    private const string MASK_PREFIX = "__$$_";
    private const string MASK_SUFFIX = "_$$__";

    /// <summary>
    /// Asynchronously evaluates a given C# expression as a string and returns the result.
    /// </summary>
    public static async Task<string> EvalAsync(string expression, object? parameter = null, DollarSignOption? option = null)
    {
        try
        {
            option ??= new DollarSignOption();
#if DEBUG
            option.EnableDebugLogging = true;
#endif

            // Handle escaped braces first
            expression = HandleEscapedBraces(expression);

            // Prepare the expression and protected patterns
            string preprocessedExpression = expression;
            List<(string original, string masked, int index)> protectedPatterns = new List<(string, string, int)>();

            if (option.SupportDollarSignSyntax)
            {
                // Dollar sign mode: evaluate ${...} and protect {...}
                Log.Debug("Dollar sign mode enabled - evaluating ${...} and protecting {...}", option);

                // Protect standard {...} patterns first
                preprocessedExpression = ProtectStandardBraces(preprocessedExpression, protectedPatterns);

                // Convert ${...} to {...} for evaluation
                preprocessedExpression = Regex.Replace(preprocessedExpression, @"\$\{([^{}]+)\}", "{$1}");
            }
            else
            {
                // Standard mode: evaluate {...} and protect ${...}
                Log.Debug("Standard mode enabled - evaluating {...} and protecting ${...}", option);

                // Protect ${...} patterns
                preprocessedExpression = ProtectDollarSignBraces(preprocessedExpression, protectedPatterns);
            }

            Log.Debug($"Preprocessed expression: {preprocessedExpression}", option);

            // Extract interpolation expressions to evaluate
            var interpolationMatches = Regex.Matches(preprocessedExpression, @"\{([^{}]+)\}");

            // If no patterns to evaluate, just restore protected parts and return
            if (interpolationMatches.Count == 0)
            {
                return RestoreProtectedPatterns(preprocessedExpression, protectedPatterns);
            }

            // Evaluate all interpolations and perform replacements
            string result = await EvaluateInterpolations(preprocessedExpression, interpolationMatches, parameter, option);

            // Restore protected patterns
            result = RestoreProtectedPatterns(result, protectedPatterns);

            return result;
        }
        catch (Exception e)
        {
            throw new DollarSignEngineException($"Error: {e.Message}", e);
        }
    }

    /// <summary>
    /// Evaluates all interpolations in an expression and performs replacements.
    /// </summary>
    private static async Task<string> EvaluateInterpolations(
        string expression,
        MatchCollection interpolationMatches,
        object? parameter,
        DollarSignOption option)
    {
        // Create collection to store replacements
        var replacements = new List<(string original, string replacement, int index, int length)>();

        // Process each match
        foreach (Match match in interpolationMatches)
        {
            string originalMatchValue = match.Value;
            string interpolationExpression = match.Groups[1].Value;

            Log.Debug($"Processing interpolation: {interpolationExpression}", option);

            // Check for format specifier
            string? formatSpecifier = null;
            int? alignment = null;

            // Extract alignment and format specifier
            (interpolationExpression, alignment, formatSpecifier) = ExtractFormatting(interpolationExpression);

            // Evaluate expression
            object? value = await EvaluateExpressionAsync(interpolationExpression, parameter, option);

            // Format result
            string formattedValue = FormatValue(value, formatSpecifier, alignment, option.CultureInfo);

            // Store for replacement
            replacements.Add((originalMatchValue, formattedValue, match.Index, match.Length));
        }

        // Perform all replacements at once (from end to avoid index changes)
        string result = expression;
        foreach (var rep in replacements.OrderByDescending(r => r.index))
        {
            result = result.Substring(0, rep.index) +
                    rep.replacement +
                    result.Substring(rep.index + rep.length);
        }

        return result;
    }

    /// <summary>
    /// Formats a value according to the format specifier and alignment.
    /// </summary>
    private static string FormatValue(object? value, string? formatSpec, int? alignment, CultureInfo? culture)
    {
        if (value == null)
        {
            return "";
        }

        // Apply format if provided
        string result;
        culture ??= CultureInfo.CurrentCulture;

        if (!string.IsNullOrEmpty(formatSpec) && value is IFormattable formattable)
        {
            result = formattable.ToString(formatSpec, culture);
        }
        else
        {
            result = Convert.ToString(value, culture) ?? "";
        }

        // Apply alignment if provided
        if (alignment.HasValue)
        {
            int spaces = Math.Abs(alignment.Value);
            if (alignment.Value > 0)
            {
                result = result.PadLeft(spaces);
            }
            else if (alignment.Value < 0)
            {
                result = result.PadRight(spaces);
            }
        }

        return result;
    }
}