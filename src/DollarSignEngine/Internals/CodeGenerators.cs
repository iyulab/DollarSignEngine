using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace DollarSignEngine.Internals;

/// <summary>
/// Contains helper methods for generating C# code
/// </summary>
internal static class CodeGenerators
{
    /// <summary>
    /// Generates code for an empty string result
    /// </summary>
    internal static string GenerateEmptyStringCode()
    {
        return @"
using System;

namespace DynamicInterpolation
{
    public static class Evaluator
    {
        public delegate object ResolverDelegate(string name);

        public static string Evaluate(ResolverDelegate resolver)
        {
            return string.Empty;
        }
    }
}";
    }

    /// <summary>
    /// Generates code for a simple string result
    /// </summary>
    internal static string GenerateSimpleStringCode(string text)
    {
        return $@"
using System;

namespace DynamicInterpolation
{{
    public static class Evaluator
    {{
        public delegate object ResolverDelegate(string name);

        public static string Evaluate(ResolverDelegate resolver)
        {{
            return ""{StringUtilities.EscapeString(text)}"";
        }}
    }}
}}";
    }

    /// <summary>
    /// Generates evaluator code for a parsed interpolated string
    /// </summary>
    internal static string GenerateEvaluatorCode(
        InterpolatedStringExpressionSyntax interpolatedString,
        HashSet<string> variablePaths)
    {
        var code = new StringBuilder();
        code.AppendLine("using System;");
        code.AppendLine("using System.Globalization;");
        code.AppendLine();
        code.AppendLine("namespace DynamicInterpolation");
        code.AppendLine("{");
        code.AppendLine("    public static class Evaluator");
        code.AppendLine("    {");
        code.AppendLine("        public delegate object ResolverDelegate(string name);");
        code.AppendLine();
        code.AppendLine("        public static string Evaluate(ResolverDelegate resolver)");
        code.AppendLine("        {");
        code.AppendLine("            try");
        code.AppendLine("            {");

        // Generate variable declarations for all identified variable paths
        foreach (var path in variablePaths)
        {
            code.AppendLine($"                object {StringUtilities.SanitizeVariableName(path)} = resolver(\"{path}\");");
        }

        // Generate the interpolated string directly
        code.Append("                return $\"");

        foreach (var content in interpolatedString.Contents)
        {
            if (content is InterpolatedStringTextSyntax textSyntax)
            {
                // Add text content directly
                code.Append(StringUtilities.EscapeString(textSyntax.TextToken.ValueText));
            }
            else if (content is InterpolationSyntax interpolation)
            {
                // Generate interpolation expression
                code.Append("{");

                // Handle different expression types for interpolation
                // For conditional expressions, we need to handle type conversion of the condition
                if (interpolation.Expression is ConditionalExpressionSyntax conditionalExpression)
                {
                    GenerateConditionalExpression(code, conditionalExpression, variablePaths);
                }
                else
                {
                    // For other expression types, replace whole variables with sanitized names
                    string expressionText = interpolation.Expression.ToString();

                    // Replace all references to variables with sanitized names 
                    foreach (var path in variablePaths.OrderByDescending(p => p.Length))
                    {
                        expressionText = StringUtilities.ReplaceWholeWord(
                            expressionText, path, StringUtilities.SanitizeVariableName(path));
                    }

                    // Fix boolean conditions in parentheses
                    expressionText = Regex.Replace(expressionText, @"\((var.*)\s*\?", (m) =>
                    {
                        return $"((bool)({m.Groups[1].Value}) ?";
                    });

                    code.Append(expressionText);
                }

                // Add alignment if present
                if (interpolation.AlignmentClause != null)
                {
                    code.Append(interpolation.AlignmentClause.ToString());
                }

                // Add format if present
                if (interpolation.FormatClause != null)
                {
                    code.Append(interpolation.FormatClause.ToString());
                }

                code.Append("}");
            }
        }

        code.AppendLine("\";");
        code.AppendLine("            }");
        code.AppendLine("            catch (Exception ex)");
        code.AppendLine("            {");
        code.AppendLine("                Console.WriteLine($\"Error in string interpolation: {ex.Message}\");");
        code.AppendLine("                return string.Empty;");
        code.AppendLine("            }");
        code.AppendLine("        }");
        code.AppendLine("    }");
        code.AppendLine("}");

        return code.ToString();
    }

    /// <summary>
    /// Generates code for a conditional expression
    /// </summary>
    private static void GenerateConditionalExpression(
        StringBuilder code,
        ConditionalExpressionSyntax conditionalExpression,
        HashSet<string> variablePaths)
    {
        // Get the condition part only
        var condition = conditionalExpression.Condition.ToString();

        // Replace all variable references in the condition
        foreach (var path in variablePaths.OrderByDescending(p => p.Length))
        {
            condition = StringUtilities.ReplaceWholeWord(
                condition, path, $"Convert.ToBoolean({StringUtilities.SanitizeVariableName(path)})");
        }

        // Get the when-true and when-false parts
        var whenTrue = conditionalExpression.WhenTrue.ToString();
        var whenFalse = conditionalExpression.WhenFalse.ToString();

        // Replace variable references in these parts too
        foreach (var path in variablePaths.OrderByDescending(p => p.Length))
        {
            whenTrue = StringUtilities.ReplaceWholeWord(
                whenTrue, path, StringUtilities.SanitizeVariableName(path));
            whenFalse = StringUtilities.ReplaceWholeWord(
                whenFalse, path, StringUtilities.SanitizeVariableName(path));
        }

        // Combine to build full conditional expression
        code.Append($"({condition} ? {whenTrue} : {whenFalse})");
    }

    /// <summary>
    /// Generates code for strings with special characters
    /// </summary>
    internal static string GenerateCodeForStringWithSpecialChars(string expression)
    {
        // Extract variable names and format specifiers from the expression
        var parser = new StringInterpolationParser();
        var parts = parser.Parse(expression);

        // Collect variable paths
        var variablePaths = new HashSet<string>();

        foreach (var part in parts)
        {
            if (part.IsVariable)
            {
                variablePaths.Add(part.Content);
            }
        }

        // Generate the evaluator code
        var code = new StringBuilder();
        code.AppendLine("using System;");
        code.AppendLine("using System.Globalization;");
        code.AppendLine();
        code.AppendLine("namespace DynamicInterpolation");
        code.AppendLine("{");
        code.AppendLine("    public static class Evaluator");
        code.AppendLine("    {");
        code.AppendLine("        public delegate object ResolverDelegate(string name);");
        code.AppendLine();
        code.AppendLine("        public static string Evaluate(ResolverDelegate resolver)");
        code.AppendLine("        {");
        code.AppendLine("            try");
        code.AppendLine("            {");

        // Generate variable declarations for all identified variables
        foreach (var path in variablePaths)
        {
            code.AppendLine($"                object {StringUtilities.SanitizeVariableName(path)} = resolver(\"{path}\");");
        }

        // Build the string using string.Concat for better control
        code.AppendLine("                return string.Concat(");

        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            code.Append("                    ");

            if (part.IsVariable)
            {
                string variableName = StringUtilities.SanitizeVariableName(part.Content);

                // Format with alignment and/or format specifier if present
                if (part.Alignment != null || part.Format != null)
                {
                    code.Append("string.Format(CultureInfo.CurrentCulture, \"{0");

                    // Add alignment if present
                    if (part.Alignment != null)
                    {
                        code.Append($",{part.Alignment}");
                    }

                    // Add format if present
                    if (part.Format != null)
                    {
                        code.Append($":{part.Format}");
                    }

                    code.Append($"}}\", {variableName})");
                }
                else
                {
                    // No formatting needed
                    code.Append($"{variableName}?.ToString() ?? string.Empty");
                }
            }
            else
            {
                // Escape the text for C# string literal
                code.Append($"\"{StringUtilities.EscapeString(part.Content)}\"");
            }

            // Add comma if not the last item
            if (i < parts.Length - 1)
            {
                code.AppendLine(",");
            }
            else
            {
                code.AppendLine();
            }
        }

        code.AppendLine("                );");
        code.AppendLine("            }");
        code.AppendLine("            catch (Exception ex)");
        code.AppendLine("            {");
        code.AppendLine("                Console.WriteLine($\"Error in string interpolation: {ex.Message}\");");
        code.AppendLine("                return string.Empty;");
        code.AppendLine("            }");
        code.AppendLine("        }");
        code.AppendLine("    }");
        code.AppendLine("}");

        return code.ToString();
    }
}