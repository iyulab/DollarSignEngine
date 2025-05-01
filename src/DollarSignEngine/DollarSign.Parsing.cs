using System.Text.RegularExpressions;

namespace DollarSignEngine;

/// <summary>
/// Contains parsing-related functionality for the DollarSign class.
/// </summary>
public static partial class DollarSign
{
    /// <summary>
    /// Extracts formatting information from an interpolation expression.
    /// </summary>
    private static (string expression, int? alignment, string? formatSpecifier) ExtractFormatting(string interpolationExpression)
    {
        // Extract alignment if present (e.g., {value,10})
        var alignMatch = Regex.Match(interpolationExpression, @"^(.+?),\s*(-?\d+)(.*)$");
        if (alignMatch.Success)
        {
            string expression = alignMatch.Groups[1].Value;
            int? alignment = int.Parse(alignMatch.Groups[2].Value);

            // Capture remaining part (might contain format specifier)
            string remaining = alignMatch.Groups[3].Value;
            string? formatSpecifier = null;

            if (!string.IsNullOrEmpty(remaining))
            {
                var formatMatchWithAlignment = Regex.Match(remaining, @"^\s*:\s*(.+)$");
                if (formatMatchWithAlignment.Success)
                {
                    formatSpecifier = formatMatchWithAlignment.Groups[1].Value;
                }
            }

            return (expression, alignment, formatSpecifier);
        }

        // Check for format specifier without alignment (e.g., {value:C2})
        var formatMatch = Regex.Match(interpolationExpression, @"^(.+?):\s*(.+)$");
        if (formatMatch.Success)
        {
            string expression = formatMatch.Groups[1].Value;
            string formatSpecifier = formatMatch.Groups[2].Value;
            return (expression, null, formatSpecifier);
        }

        // No formatting or alignment
        return (interpolationExpression, null, null);
    }

    /// <summary>
    /// Handles escaped braces in the interpolation string.
    /// </summary>
    private static string HandleEscapedBraces(string expression)
    {
        // Replace {{ with a temporary placeholder
        expression = expression.Replace("{{", "~~OPEN_BRACE~~");
        // Replace }} with a temporary placeholder
        expression = expression.Replace("}}", "~~CLOSE_BRACE~~");
        return expression;
    }

    /// <summary>
    /// Restores escaped braces from temporary placeholders.
    /// </summary>
    private static string RestoreEscapedBraces(string expression)
    {
        // Restore {{ from temporary placeholder
        expression = expression.Replace("~~OPEN_BRACE~~", "{");
        // Restore }} from temporary placeholder
        expression = expression.Replace("~~CLOSE_BRACE~~", "}");
        return expression;
    }

    /// <summary>
    /// Protects standard {...} patterns in an expression.
    /// </summary>
    private static string ProtectStandardBraces(
        string expression,
        List<(string original, string masked, int index)> protectedPatterns)
    {
        var standardMatches = Regex.Matches(expression, @"(?<!\$)\{([^{}]+)\}");
        int maskCount = 0;
        string result = expression;

        // Process from end to avoid index changes
        for (int i = standardMatches.Count - 1; i >= 0; i--)
        {
            Match match = standardMatches[i];

            // Extra check: make sure it's not part of a ${...} pattern
            if (match.Index == 0 || expression[match.Index - 1] != '$')
            {
                // Create unique mask
                string masked = $"{MASK_PREFIX}{maskCount++}{MASK_SUFFIX}";

                // Store original string, mask, and position
                protectedPatterns.Add((match.Value, masked, match.Index));

                // Replace at exact position
                result = result.Substring(0, match.Index) +
                         masked +
                         result.Substring(match.Index + match.Length);
            }
        }

        return result;
    }

    /// <summary>
    /// Protects ${...} patterns in an expression.
    /// </summary>
    private static string ProtectDollarSignBraces(
        string expression,
        List<(string original, string masked, int index)> protectedPatterns)
    {
        var dollarMatches = Regex.Matches(expression, @"\$\{([^{}]+)\}");
        int maskCount = 0;
        string result = expression;

        // Process from end to avoid index changes
        for (int i = dollarMatches.Count - 1; i >= 0; i--)
        {
            Match match = dollarMatches[i];

            // Create unique mask
            string masked = $"{MASK_PREFIX}{maskCount++}{MASK_SUFFIX}";

            // Store original string, mask, and position
            protectedPatterns.Add((match.Value, masked, match.Index));

            // Replace at exact position
            result = result.Substring(0, match.Index) +
                     masked +
                     result.Substring(match.Index + match.Length);
        }

        return result;
    }

    /// <summary>
    /// Restores protected patterns in an expression.
    /// </summary>
    private static string RestoreProtectedPatterns(
        string expression,
        List<(string original, string masked, int index)> protectedPatterns)
    {
        string result = expression;

        // Restore masked values to original
        foreach (var pattern in protectedPatterns)
        {
            result = result.Replace(pattern.masked, pattern.original);
        }

        // Restore escaped braces
        result = RestoreEscapedBraces(result);

        return result;
    }
}