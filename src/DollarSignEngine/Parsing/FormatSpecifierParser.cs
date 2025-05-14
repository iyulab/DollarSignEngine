namespace DollarSignEngine.Parsing;

/// <summary>
/// Parses format specifiers and alignment in interpolated expressions.
/// </summary>
internal class FormatSpecifierParser
{
    // Regex for matching balanced parentheses
    private static readonly Regex BalancedParenthesesRegex = new(@"\((?:[^()]*|\((?:[^()]*|\([^()]*\))*\))*\)", RegexOptions.Compiled);

    /// <summary>
    /// Extracts formatting information from an interpolation expression.
    /// </summary>
    public (string Expression, int? Alignment, string? FormatSpecifier) Parse(string expression)
    {
        // First handle the case where we have a ternary operator
        if (ContainsTernaryOperator(expression))
        {
            // Check for format specifier after a ternary expression
            var ternaryWithFormatMatch = Regex.Match(expression, @"^(.+?\?.+?:.+?):\s*([^:]+)$");
            if (ternaryWithFormatMatch.Success)
            {
                string expr = ternaryWithFormatMatch.Groups[1].Value;
                string formatSpecifier = ternaryWithFormatMatch.Groups[2].Value;
                return (expr, null, formatSpecifier);
            }

            // For ternary operators, don't try to extract format specifiers from the expression itself
            return (expression, null, null);
        }

        // Extract alignment if present (e.g., {value,10})
        var alignMatch = Regex.Match(expression, @"^(.+?),\s*(-?\d+)(.*)$");
        if (alignMatch.Success)
        {
            string expr = alignMatch.Groups[1].Value;
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

            return (expr, alignment, formatSpecifier);
        }

        // Check for format specifier without alignment (e.g., {value:C2})
        var formatMatch = Regex.Match(expression, @"^(.+?):\s*(.+)$");
        if (formatMatch.Success && !formatMatch.Groups[1].Value.Contains("?"))
        {
            string expr = formatMatch.Groups[1].Value;
            string formatSpecifier = formatMatch.Groups[2].Value;
            return (expr, null, formatSpecifier);
        }

        // No formatting or alignment
        return (expression, null, null);
    }

    /// <summary>
    /// Checks if an expression contains a ternary operator.
    /// </summary>
    private bool ContainsTernaryOperator(string expression)
    {
        // Quick check for ? and : characters
        if (!expression.Contains('?') || !expression.Contains(':'))
            return false;

        int questionMarkLevel = 0;
        int colonLevel = 0;
        bool inString = false;
        char stringDelimiter = '\0';

        // Track parentheses nesting level to handle complex expressions
        int parenthesisLevel = 0;

        for (int i = 0; i < expression.Length; i++)
        {
            char current = expression[i];

            // Handle string literals
            if ((current == '"' || current == '\'') && (i == 0 || expression[i - 1] != '\\'))
            {
                if (!inString)
                {
                    inString = true;
                    stringDelimiter = current;
                }
                else if (current == stringDelimiter)
                {
                    inString = false;
                }
                continue;
            }

            // Skip characters inside string literals
            if (inString)
                continue;

            // Track parentheses nesting
            if (current == '(')
            {
                parenthesisLevel++;
            }
            else if (current == ')')
            {
                parenthesisLevel--;
            }
            else if (current == '?' && parenthesisLevel == 0)
            {
                questionMarkLevel++;
            }
            else if (current == ':' && parenthesisLevel == 0)
            {
                colonLevel++;

                // A valid ternary operator has a question mark before its colon
                if (questionMarkLevel > 0)
                    return true;
            }
        }

        // For a valid ternary expression, we should have both ? and : outside of strings
        return questionMarkLevel > 0 && colonLevel > 0;
    }

    /// <summary>
    /// Ensures that a ternary expression is complete with matching ? and : symbols.
    /// </summary>
    public bool IsCompleteTernaryExpression(string expression)
    {
        if (!ContainsTernaryOperator(expression))
            return false;

        // Count balanced ? and : characters outside of string literals and parentheses
        int questionMarkCount = 0;
        int colonCount = 0;
        bool inString = false;
        char stringDelimiter = '\0';
        int parenthesisLevel = 0;

        for (int i = 0; i < expression.Length; i++)
        {
            char current = expression[i];

            // Handle string literals
            if ((current == '"' || current == '\'') && (i == 0 || expression[i - 1] != '\\'))
            {
                if (!inString)
                {
                    inString = true;
                    stringDelimiter = current;
                }
                else if (current == stringDelimiter)
                {
                    inString = false;
                }
                continue;
            }

            // Skip characters inside string literals
            if (inString)
                continue;

            // Track parentheses nesting
            if (current == '(')
            {
                parenthesisLevel++;
            }
            else if (current == ')')
            {
                parenthesisLevel--;
            }
            else if (current == '?' && parenthesisLevel == 0)
            {
                questionMarkCount++;
            }
            else if (current == ':' && parenthesisLevel == 0)
            {
                colonCount++;
            }
        }

        // For a complete ternary expression, each ? must have a corresponding :
        return questionMarkCount > 0 && questionMarkCount <= colonCount;
    }
}