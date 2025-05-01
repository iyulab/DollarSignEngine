using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Text;

namespace DollarSignEngine;

/// <summary>
/// Provides string interpolation parsing using Roslyn API.
/// </summary>
internal static class InterpolationParser
{
    /// <summary>
    /// Processes and validates the interpolation expression.
    /// </summary>
    public static string ProcessInterpolation(string expression)
    {
        // Check if the expression is already in the correct format
        bool hasPrefix = expression.StartsWith("$");
        bool isQuoted = (hasPrefix && expression.StartsWith("$\"") && expression.EndsWith("\"")) ||
                        (!hasPrefix && expression.StartsWith("\"") && expression.EndsWith("\""));

        // If it's already a properly formatted interpolated string, return as is
        if (hasPrefix && isQuoted)
        {
            return expression;
        }

        // If it's quoted but missing the $ prefix
        if (isQuoted && !hasPrefix)
        {
            return "$" + expression;
        }

        // If it's not quoted or not properly formatted, process it
        return CreateValidInterpolatedString(expression);
    }

    /// <summary>
    /// Creates a valid interpolated string by using Roslyn to parse and validate the expression.
    /// </summary>
    private static string CreateValidInterpolatedString(string expression)
    {
        // Try to parse as interpolated string using Roslyn
        string wrappedExpression = $"$\"{expression}\"";

        try
        {
            // Parse the expression as a complete statement to check validity
            var testCode = $"var test = {wrappedExpression};";
            var syntaxTree = CSharpSyntaxTree.ParseText(testCode);
            var diagnostics = syntaxTree.GetDiagnostics();

            // If there are no errors, the interpolated string is valid
            if (!diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            {
                return wrappedExpression;
            }

            // If there are errors but they're not related to braces, still use basic wrapping
            var braceErrors = diagnostics.Where(d =>
                d.Id == "CS8076" || // Missing close delimiter '}' for interpolated expression
                d.Id == "CS1026"    // Expected '}'
            );

            if (!braceErrors.Any())
            {
                return wrappedExpression;
            }

            // If there are brace errors, use token-based approach
            return TokenizeAndProcessInterpolation(expression);
        }
        catch
        {
            // If Roslyn parsing fails, fall back to token-based approach
            return TokenizeAndProcessInterpolation(expression);
        }
    }

    /// <summary>
    /// Tokenizes and processes the interpolation expression to handle complex scenarios.
    /// </summary>
    private static string TokenizeAndProcessInterpolation(string expression)
    {
        var parts = TokenizeExpression(expression);
        var result = new StringBuilder("$\"");

        foreach (var part in parts)
        {
            if (part.IsExpression)
            {
                // Validate and clean up the expression part
                var cleanExpression = ValidateExpressionPart(part.Content);
                result.Append("{").Append(cleanExpression).Append("}");
            }
            else
            {
                // Escape string content
                result.Append(EscapeStringLiteral(part.Content));
            }
        }

        result.Append("\"");
        return result.ToString();
    }

    /// <summary>
    /// Validates and cleans up an expression part to ensure it's valid C#.
    /// </summary>
    private static string ValidateExpressionPart(string expressionContent)
    {
        // Remove outer braces if present
        string content = expressionContent;
        if (content.StartsWith("{") && content.EndsWith("}"))
        {
            content = content.Substring(1, content.Length - 2);
        }

        // Try to parse the expression to check validity
        try
        {
            var testCode = $"var test = {content};";
            var syntaxTree = CSharpSyntaxTree.ParseText(testCode);
            var diagnostics = syntaxTree.GetDiagnostics();

            if (!diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            {
                return content;
            }

            // If there are errors, try to fix common issues

            // Check for unbalanced quotes
            var quoteCount = content.Count(c => c == '"' && !IsEscaped(content, content.IndexOf(c)));
            if (quoteCount % 2 != 0)
            {
                // Try to fix by adding or escaping quotes
                content = FixUnbalancedQuotes(content);
            }

            // Check for unbalanced parentheses
            if (CountChar(content, '(') != CountChar(content, ')'))
            {
                // Try to fix by balancing parentheses
                content = BalanceParentheses(content);
            }

            return content;
        }
        catch
        {
            // If validation fails, return the content as is, letting the script evaluation handle any errors
            return content;
        }
    }

    /// <summary>
    /// Tokenizes an expression into string parts and expression parts.
    /// </summary>
    private static List<InterpolationPart> TokenizeExpression(string expression)
    {
        var parts = new List<InterpolationPart>();
        var currentText = new StringBuilder();
        bool inExpression = false;
        int braceDepth = 0;
        bool inString = false;
        char stringDelimiter = '\0';
        bool escaped = false;

        for (int i = 0; i < expression.Length; i++)
        {
            char c = expression[i];

            // Handle escape sequences
            if (c == '\\' && !escaped)
            {
                escaped = true;
                currentText.Append(c);
                continue;
            }

            // Handle string literals
            if ((c == '"' || c == '\'') && !escaped)
            {
                if (!inString)
                {
                    inString = true;
                    stringDelimiter = c;
                }
                else if (c == stringDelimiter)
                {
                    inString = false;
                }

                currentText.Append(c);
                escaped = false;
                continue;
            }

            // Handle braces when not in a string literal
            if (!inString && !escaped)
            {
                if (c == '{')
                {
                    // Check for escaped braces
                    if (i + 1 < expression.Length && expression[i + 1] == '{')
                    {
                        currentText.Append("{{");
                        i++; // Skip the next brace
                    }
                    else
                    {
                        // Start of expression
                        if (!inExpression)
                        {
                            if (currentText.Length > 0)
                            {
                                parts.Add(new InterpolationPart { Content = currentText.ToString(), IsExpression = false });
                                currentText.Clear();
                            }
                            inExpression = true;
                        }

                        braceDepth++;
                        currentText.Append(c);
                    }

                    escaped = false;
                    continue;
                }

                if (c == '}')
                {
                    // Check for escaped braces
                    if (i + 1 < expression.Length && expression[i + 1] == '}')
                    {
                        currentText.Append("}}");
                        i++; // Skip the next brace
                    }
                    else if (inExpression)
                    {
                        braceDepth--;
                        currentText.Append(c);

                        // End of expression
                        if (braceDepth == 0)
                        {
                            parts.Add(new InterpolationPart { Content = currentText.ToString(), IsExpression = true });
                            currentText.Clear();
                            inExpression = false;
                        }
                    }
                    else
                    {
                        currentText.Append(c);
                    }

                    escaped = false;
                    continue;
                }
            }

            // Handle normal characters
            currentText.Append(c);
            escaped = false;
        }

        // Add any remaining text
        if (currentText.Length > 0)
        {
            parts.Add(new InterpolationPart { Content = currentText.ToString(), IsExpression = inExpression });
        }

        // Handle case where we might end with an unclosed expression
        if (inExpression)
        {
            // Try to automatically close any unclosed expressions
            var lastPart = parts.LastOrDefault();
            if (lastPart != null && lastPart.IsExpression)
            {
                string content = lastPart.Content;
                // Count unmatched opening braces
                int unmatchedBraces = CountChar(content, '{') - CountChar(content, '}');

                // Add closing braces if needed
                if (unmatchedBraces > 0)
                {
                    content = content + new string('}', unmatchedBraces);
                    parts[parts.Count - 1] = new InterpolationPart { Content = content, IsExpression = true };
                }
            }
        }

        return parts;
    }

    /// <summary>
    /// Escapes special characters in a string literal.
    /// </summary>
    private static string EscapeStringLiteral(string text)
    {
        return text
            .Replace("\\", "\\\\")  // Must be first to avoid double-escaping
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }

    /// <summary>
    /// Checks if a character at a given position is escaped.
    /// </summary>
    private static bool IsEscaped(string text, int position)
    {
        if (position <= 0) return false;

        // Count preceding backslashes
        int backslashCount = 0;
        int index = position - 1;

        while (index >= 0 && text[index] == '\\')
        {
            backslashCount++;
            index--;
        }

        // If odd number of backslashes, the character is escaped
        return backslashCount % 2 == 1;
    }

    /// <summary>
    /// Counts occurrences of a specific character in a string.
    /// </summary>
    private static int CountChar(string text, char c)
    {
        return text.Count(ch => ch == c);
    }

    /// <summary>
    /// Fixes unbalanced quotes in an expression.
    /// </summary>
    private static string FixUnbalancedQuotes(string content)
    {
        var result = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < content.Length; i++)
        {
            char c = content[i];

            if (c == '"' && !IsEscaped(content, i))
            {
                inQuotes = !inQuotes;
            }

            // Add the character to the result
            result.Append(c);
        }

        // If we ended in an open quote state, add a closing quote
        if (inQuotes)
        {
            result.Append('"');
        }

        return result.ToString();
    }

    /// <summary>
    /// Balances parentheses in an expression.
    /// </summary>
    private static string BalanceParentheses(string content)
    {
        int openCount = CountChar(content, '(');
        int closeCount = CountChar(content, ')');

        // Add missing closing parentheses
        if (openCount > closeCount)
        {
            return content + new string(')', openCount - closeCount);
        }

        // Add missing opening parentheses at the beginning
        if (closeCount > openCount)
        {
            return new string('(', closeCount - openCount) + content;
        }

        return content;
    }

    /// <summary>
    /// Represents a part of an interpolation expression.
    /// </summary>
    private class InterpolationPart
    {
        public string Content { get; set; } = string.Empty;
        public bool IsExpression { get; set; }
    }
}