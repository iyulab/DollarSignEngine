using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace DollarSignEngine;

/// <summary>
/// Compiles string interpolation expressions using Roslyn
/// </summary>
internal class DollarSignCompiler
{
    private readonly Dictionary<string, CompiledExpression> _cache = new();
    private readonly object _cacheLock = new();

    /// <summary>
    /// Compiles a string interpolation expression
    /// </summary>
    internal async Task<CompiledExpression> CompileExpressionAsync(
        string expression,
        DollarSignOptions options)
    {
        // Try to get from cache if enabled
        string cacheKey = expression;
        if (options.UseCache && TryGetFromCache(cacheKey, out var cached))
            return cached;

        try
        {
            // Generate source code
            string sourceCode = GenerateSourceCode(expression);
            Console.WriteLine($"Generated source code for expression: {sourceCode}");
            // Compile the source code
            Assembly assembly;
            try
            {
                assembly = await CompileSourceCodeAsync(sourceCode);
            }
            catch (Exception ex)
            {
                throw new CompilationException($"Failed to compile expression: {ex.Message}", sourceCode);
            }

            // Create compiled expression wrapper
            var compiledExpression = new CompiledExpression(assembly);

            // Add to cache if option enabled
            if (options.UseCache)
                AddToCache(cacheKey, compiledExpression);

            return compiledExpression;
        }
        catch (CompilationException)
        {
            throw;
        }
        catch (DollarSignEngineException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CompilationException($"Failed to compile expression: {ex.Message}", expression);
        }
    }

    // Simple class to parse interpolation expressions
    private class SimpleInterpolationParser
    {
        public class InterpolationPart
        {
            public string Content { get; set; }
            public bool IsVariable { get; set; }

            public InterpolationPart(string content, bool isVariable)
            {
                Content = content;
                IsVariable = isVariable;
            }
        }

        public InterpolationPart[] Parse(string template)
        {
            var result = new List<InterpolationPart>();
            int pos = 0;

            while (pos < template.Length)
            {
                // Find the next variable
                int openBrace = template.IndexOf('{', pos);

                if (openBrace == -1)
                {
                    // No more variables, add the rest as text
                    if (pos < template.Length)
                    {
                        result.Add(new InterpolationPart(template.Substring(pos), false));
                    }
                    break;
                }

                // Check if it's an escaped brace
                if (openBrace + 1 < template.Length && template[openBrace + 1] == '{')
                {
                    // Add text up to and including one brace
                    result.Add(new InterpolationPart(template.Substring(pos, openBrace - pos + 1), false));
                    pos = openBrace + 2; // Skip both braces
                    continue;
                }

                // Add text before the variable
                if (openBrace > pos)
                {
                    result.Add(new InterpolationPart(template.Substring(pos, openBrace - pos), false));
                }

                // Find the closing brace
                int closeBrace = template.IndexOf('}', openBrace + 1);
                if (closeBrace == -1)
                {
                    // No closing brace, treat the rest as text
                    result.Add(new InterpolationPart(template.Substring(pos), false));
                    break;
                }

                // Extract the variable name
                string varName = template.Substring(openBrace + 1, closeBrace - openBrace - 1).Trim();
                result.Add(new InterpolationPart(varName, true));

                // Move past the closing brace
                pos = closeBrace + 1;
            }

            return result.ToArray();
        }
    }

    private string GenerateSourceCode(string expression)
    {
        if (string.IsNullOrEmpty(expression))
            return GenerateEmptyStringCode();

        // For strings with newlines or tabs, we need a different approach
        if (expression.Contains('\n') || expression.Contains('\r') || expression.Contains('\t'))
        {
            return GenerateCodeForStringWithSpecialChars(expression);
        }

        // Regular approach for strings without special characters
        string wrappedExpression = $"$\"{expression}\"";

        // Use Roslyn to parse the interpolated string expression
        var tree = CSharpSyntaxTree.ParseText(wrappedExpression);
        var root = tree.GetRoot();

        // Find all interpolated string expressions in the parsed code
        var interpolatedString = root.DescendantNodes().OfType<InterpolatedStringExpressionSyntax>().FirstOrDefault();
        if (interpolatedString == null)
        {
            // If not an interpolated string, return simple string code
            return GenerateSimpleStringCode(expression);
        }

        // Extract all identifiers from the interpolation expressions
        var variablePaths = new HashSet<string>();
        var formatSpecifiers = new Dictionary<string, string>(); // Map of variable path to format specifier
        var interpolations = interpolatedString.DescendantNodes().OfType<InterpolationSyntax>();

        foreach (var interpolation in interpolations)
        {
            // Extract all identifiers from this interpolation
            ExtractAllIdentifiers(interpolation.Expression, variablePaths);

            // Also extract format specifiers if present
            if (interpolation.FormatClause != null)
            {
                string format = interpolation.FormatClause.ToString().TrimStart(':');

                // Associate format with variable if it's a simple variable
                if (interpolation.Expression is IdentifierNameSyntax identifier)
                {
                    formatSpecifiers[identifier.Identifier.Text] = format;
                }
                else if (interpolation.Expression is MemberAccessExpressionSyntax memberAccess)
                {
                    string path = ExtractFullPropertyPath(memberAccess);
                    if (!string.IsNullOrEmpty(path))
                    {
                        formatSpecifiers[path] = format;
                    }
                }
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

        // Generate variable declarations for all identified variable paths
        foreach (var path in variablePaths)
        {
            code.AppendLine($"                object {SanitizeVariableName(path)} = resolver(\"{path}\");");
        }

        // Generate the interpolated string directly
        code.Append("                return $\"");

        foreach (var content in interpolatedString.Contents)
        {
            if (content is InterpolatedStringTextSyntax textSyntax)
            {
                // Add text content directly
                code.Append(EscapeString(textSyntax.TextToken.ValueText));
            }
            else if (content is InterpolationSyntax interpolation)
            {
                // Generate interpolation expression
                code.Append("{");

                // Handle different expression types for interpolation
                // For conditional expressions, we need to handle type conversion of the condition
                if (interpolation.Expression is ConditionalExpressionSyntax conditionalExpression)
                {
                    // Get the condition part only
                    var condition = conditionalExpression.Condition.ToString();

                    // Replace all variable references in the condition
                    foreach (var path in variablePaths.OrderByDescending(p => p.Length))
                    {
                        condition = ReplaceWholeWord(condition, path, $"Convert.ToBoolean({SanitizeVariableName(path)})");
                    }

                    // Get the when-true and when-false parts
                    var whenTrue = conditionalExpression.WhenTrue.ToString();
                    var whenFalse = conditionalExpression.WhenFalse.ToString();

                    // Replace variable references in these parts too
                    foreach (var path in variablePaths.OrderByDescending(p => p.Length))
                    {
                        whenTrue = ReplaceWholeWord(whenTrue, path, SanitizeVariableName(path));
                        whenFalse = ReplaceWholeWord(whenFalse, path, SanitizeVariableName(path));
                    }

                    // Combine to build full conditional expression
                    code.Append($"({condition} ? {whenTrue} : {whenFalse})");
                }
                else
                {
                    // For other expression types, replace whole variables with sanitized names
                    string expressionText = interpolation.Expression.ToString();

                    // Replace all references to variables with sanitized names 
                    foreach (var path in variablePaths.OrderByDescending(p => p.Length))
                    {
                        expressionText = ReplaceWholeWord(expressionText, path, SanitizeVariableName(path));
                    }

                    
                    expressionText = Regex.Replace(expressionText, @"\((var.*).s*\?", (m) =>
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

    private void ExtractAllIdentifiers(ExpressionSyntax expression, HashSet<string> identifiers)
    {
        // Process simple identifiers
        if (expression is IdentifierNameSyntax identifier)
        {
            identifiers.Add(identifier.Identifier.Text);
            return;
        }

        // Process member access expressions (e.g., person.Name)
        if (expression is MemberAccessExpressionSyntax memberAccess)
        {
            string path = ExtractFullPropertyPath(memberAccess);
            if (!string.IsNullOrEmpty(path))
            {
                identifiers.Add(path);

                // Also add the root object name in case it's used elsewhere
                if (memberAccess.Expression is IdentifierNameSyntax rootIdentifier)
                {
                    identifiers.Add(rootIdentifier.Identifier.Text);
                }
            }
            return;
        }

        // For all other expressions, find all contained identifiers
        foreach (var node in expression.DescendantNodesAndSelf())
        {
            if (node is IdentifierNameSyntax childId && node != expression)
            {
                identifiers.Add(childId.Identifier.Text);
            }
            else if (node is MemberAccessExpressionSyntax childMemberAccess && node != expression)
            {
                string path = ExtractFullPropertyPath(childMemberAccess);
                if (!string.IsNullOrEmpty(path))
                {
                    identifiers.Add(path);

                    // Also add the root object name in case it's used elsewhere
                    if (childMemberAccess.Expression is IdentifierNameSyntax rootIdentifier)
                    {
                        identifiers.Add(rootIdentifier.Identifier.Text);
                    }
                }
            }
        }
    }

    // Extract the variable name, alignment, and format specifier from an interpolation expression
    private void ExtractVariableAndFormat(InterpolationSyntax interpolation, HashSet<string> variablePaths, Dictionary<string, string> formatSpecifiers)
    {
        // For complex expressions like ternary operators, we won't extract variables
        if (interpolation.Expression is ConditionalExpressionSyntax)
        {
            // It's a ternary operator, we'll handle it directly in the generated code
            return;
        }

        // Get the expression text without any alignment or format
        string expressionText = GetExpressionWithoutAlignmentAndFormat(interpolation.Expression);

        // Get the format specifier if present
        string format = null;
        if (interpolation.FormatClause != null)
        {
            format = interpolation.FormatClause.ToString().TrimStart(':');
        }

        // Get the alignment specifier if present
        string alignment = null;
        if (interpolation.AlignmentClause != null)
        {
            alignment = interpolation.AlignmentClause.ToString().TrimStart(',');
        }

        // Now extract the variable path from the expression
        string variablePath = null;

        // Handle simple identifier
        if (interpolation.Expression is IdentifierNameSyntax identifier)
        {
            variablePath = identifier.Identifier.Text;
        }
        // Handle member access expression
        else if (interpolation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            variablePath = ExtractFullPropertyPath(memberAccess);
        }
        // Handle more complex cases - try to extract from the expression text
        else
        {
            variablePath = GetVariablePath(expressionText);
        }

        // Add to our collections if we found a variable path
        if (!string.IsNullOrEmpty(variablePath))
        {
            variablePaths.Add(variablePath);

            // Store the format specifier if we have one
            if (!string.IsNullOrEmpty(format))
            {
                formatSpecifiers[variablePath] = format;
            }
        }
    }

    // Get the expression text without any alignment or format specifiers
    private string GetExpressionWithoutAlignmentAndFormat(ExpressionSyntax expression)
    {
        // If it's a conditional expression (ternary operator), return the full text
        if (expression is ConditionalExpressionSyntax)
        {
            return expression.ToString();
        }

        string text = expression.ToString();

        // If contains a comma, it might be an alignment specifier
        // But be careful - commas could be part of a method call's parameters too
        if (text.Contains(',') && !expression.DescendantNodes().OfType<ArgumentSyntax>().Any())
        {
            text = text.Split(new[] { ',' }, 2)[0];
        }

        // If contains a colon, it might be a format specifier
        // But be careful - this could be part of a ternary operator
        if (text.Contains(':') && !expression.DescendantNodes().OfType<ConditionalExpressionSyntax>().Any())
        {
            text = text.Split(new[] { ':' }, 2)[0];
        }

        return text;
    }

    // Extract the variable path from an expression text
    private string GetVariablePath(string expressionText)
    {
        // Handle simple variable
        if (!expressionText.Contains('.') && !expressionText.Contains('(') &&
            !expressionText.Contains(')') && !expressionText.Contains(' '))
        {
            return expressionText;
        }

        // Handle member access (simple case)
        if (expressionText.Contains('.') && !expressionText.Contains('(') &&
            !expressionText.Contains(')') && !expressionText.Contains(' '))
        {
            return expressionText;
        }

        // More complex expressions - we'd need more sophisticated parsing
        return null;
    }

    // Get the expression text without any format specifier
    private string GetExpressionWithoutFormat(ExpressionSyntax expression)
    {
        string text = expression.ToString();

        // If contains a colon, it might be a format specifier
        if (text.Contains(':'))
        {
            return text.Split(new[] { ':' }, 2)[0];
        }

        return text;
    }

    // Special version to handle strings with special characters and format specifiers
    private string GenerateCodeForStringWithSpecialChars(string expression)
    {
        // Extract variable names and format specifiers from the expression
        var parser = new StringInterpolationParser();
        var parts = parser.Parse(expression);

        // Collect variable paths and format specifiers
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
            code.AppendLine($"                object {SanitizeVariableName(path)} = resolver(\"{path}\");");
        }

        // Build the string using string.Concat for better control
        code.AppendLine("                return string.Concat(");

        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            code.Append("                    ");

            if (part.IsVariable)
            {
                string variableName = SanitizeVariableName(part.Content);

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
                code.Append($"\"{EscapeString(part.Content)}\"");
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

    // Improved parser class for string interpolation that handles format and alignment specifiers
    private class StringInterpolationParser
    {
        public class InterpolationPart
        {
            public string Content { get; set; }
            public bool IsVariable { get; set; }
            public string? Alignment { get; set; }
            public string? Format { get; set; }

            public InterpolationPart(string content, bool isVariable, string? alignment = null, string? format = null)
            {
                Content = content;
                IsVariable = isVariable;
                Alignment = alignment;
                Format = format;
            }
        }

        public InterpolationPart[] Parse(string template)
        {
            var result = new List<InterpolationPart>();
            int pos = 0;

            while (pos < template.Length)
            {
                // Find the next variable
                int openBrace = template.IndexOf('{', pos);

                if (openBrace == -1)
                {
                    // No more variables, add the rest as text
                    if (pos < template.Length)
                    {
                        result.Add(new InterpolationPart(template.Substring(pos), false));
                    }
                    break;
                }

                // Check if it's an escaped brace
                if (openBrace + 1 < template.Length && template[openBrace + 1] == '{')
                {
                    // Add text up to and including one brace
                    result.Add(new InterpolationPart(template.Substring(pos, openBrace - pos + 1), false));
                    pos = openBrace + 2; // Skip both braces
                    continue;
                }

                // Add text before the variable
                if (openBrace > pos)
                {
                    result.Add(new InterpolationPart(template.Substring(pos, openBrace - pos), false));
                }

                // Find the closing brace
                int closeBrace = template.IndexOf('}', openBrace + 1);
                if (closeBrace == -1)
                {
                    // No closing brace, treat the rest as text
                    result.Add(new InterpolationPart(template.Substring(pos), false));
                    break;
                }

                // Extract the variable expression - this could include alignment and format specifiers
                string varExpression = template.Substring(openBrace + 1, closeBrace - openBrace - 1).Trim();

                // Parse for alignment and format specifiers
                string variableName = varExpression;
                string? alignment = null;
                string? format = null;

                // Check for format specifier first (comes after a colon)
                if (variableName.Contains(':'))
                {
                    var segments = variableName.Split(':', 2);
                    variableName = segments[0].Trim();
                    format = segments[1].Trim();
                }

                // Check for alignment specifier (comes after a comma)
                if (variableName.Contains(','))
                {
                    var segments = variableName.Split(',', 2);
                    variableName = segments[0].Trim();
                    alignment = segments[1].Trim();
                }

                result.Add(new InterpolationPart(variableName, true, alignment, format));

                // Move past the closing brace
                pos = closeBrace + 1;
            }

            return result.ToArray();
        }
    }

    // Extract full property paths from expression (including dotted paths)
    private void ExtractPropertyPaths(ExpressionSyntax expression, HashSet<string> paths)
    {
        // Case 1: Simple identifier - just a variable name
        if (expression is IdentifierNameSyntax identifier)
        {
            paths.Add(identifier.Identifier.Text);
            return;
        }

        // Case 2: Member access - nested property like Address.City
        if (expression is MemberAccessExpressionSyntax memberAccess)
        {
            // Extract the full path (Address.City)
            string path = ExtractFullPropertyPath(memberAccess);
            if (!string.IsNullOrEmpty(path))
            {
                paths.Add(path);
            }
            return;
        }

        // Recursively process all contained expressions
        foreach (var childNode in expression.DescendantNodesAndSelf().OfType<ExpressionSyntax>())
        {
            if (childNode is IdentifierNameSyntax childIdentifier)
            {
                paths.Add(childIdentifier.Identifier.Text);
            }
            else if (childNode is MemberAccessExpressionSyntax childMemberAccess)
            {
                string path = ExtractFullPropertyPath(childMemberAccess);
                if (!string.IsNullOrEmpty(path))
                {
                    paths.Add(path);
                }
            }
        }
    }

    // Extract the full path from a member access expression (e.g., "Address.City")
    private string ExtractFullPropertyPath(MemberAccessExpressionSyntax memberAccess)
    {
        // Build the path from right to left (City <- Address)
        var parts = new List<string>();

        // Add the rightmost identifier (the property name)
        parts.Add(memberAccess.Name.Identifier.Text);

        // Walk up the tree to get the full path
        ExpressionSyntax current = memberAccess.Expression;

        while (true)
        {
            if (current is IdentifierNameSyntax identifier)
            {
                // Found the root identifier, add it and break
                parts.Add(identifier.Identifier.Text);
                break;
            }
            else if (current is MemberAccessExpressionSyntax nestedMemberAccess)
            {
                // Add this segment and continue with the expression
                parts.Add(nestedMemberAccess.Name.Identifier.Text);
                current = nestedMemberAccess.Expression;
            }
            else
            {
                // Not a simple property path, return empty
                return string.Empty;
            }
        }

        // Reverse and join with dots
        parts.Reverse();
        return string.Join(".", parts);
    }

    // Replace a property path with a variable name in an expression
    private string ReplacePropertyPath(string expression, string path, string variableName)
    {
        // Split the path to handle nested properties
        var parts = path.Split('.');

        if (parts.Length == 1)
        {
            // Simple variable, just replace if it's a whole word
            return ReplaceWholeWord(expression, path, variableName);
        }

        // For nested properties, we need to match the exact path
        if (expression == path)
        {
            return variableName;
        }

        // Otherwise, leave it as is
        return expression;
    }

    /// <summary>
    /// Sanitizes a variable name for use in generated code
    /// </summary>
    private string SanitizeVariableName(string name)
    {
        // Ensure variable names don't conflict with C# keywords
        // and are valid identifiers
        return $"var_{name.Replace(".", "_")}";
    }

    /// <summary>
    /// Replaces a whole word in a string
    /// </summary>
    private string ReplaceWholeWord(string text, string oldWord, string newWord)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(oldWord))
            return text;

        return System.Text.RegularExpressions.Regex.Replace(
            text,
            $@"\b{System.Text.RegularExpressions.Regex.Escape(oldWord)}\b",
            match => {
                // Make sure we're not replacing part of a property access
                int position = match.Index;
                bool hasDotBefore = position > 0 && text[position - 1] == '.';
                bool hasDotAfter = position + oldWord.Length < text.Length && text[position + oldWord.Length] == '.';

                if (hasDotBefore || hasDotAfter)
                    return match.Value;

                return newWord;
            });
    }

    /// <summary>
    /// Escapes special characters in a string for C# code generation
    /// </summary>
    private string EscapeString(string text)
    {
        return text
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }

    /// <summary>
    /// Checks if a string matches a standalone identifier
    /// </summary>
    private bool IsStandaloneIdentifier(string text, string identifier)
    {
        int index = text.IndexOf(identifier);
        if (index < 0) return false;

        bool validStart = index == 0 || !char.IsLetterOrDigit(text[index - 1]) && text[index - 1] != '_';
        bool validEnd = index + identifier.Length == text.Length ||
                        !char.IsLetterOrDigit(text[index + identifier.Length]) &&
                        text[index + identifier.Length] != '_';

        return validStart && validEnd;
    }

    /// <summary>
    /// Collects variable names from a syntax node
    /// </summary>
    private void CollectVariableNames(ExpressionSyntax expression, HashSet<string> variables)
    {
        // Handle identifier names (simple variables)
        if (expression is IdentifierNameSyntax identifier)
        {
            variables.Add(identifier.Identifier.Text);
            return;
        }

        // Recursively process all contained expressions
        foreach (var childNode in expression.DescendantNodesAndSelf().OfType<ExpressionSyntax>())
        {
            if (childNode is IdentifierNameSyntax childIdentifier)
            {
                variables.Add(childIdentifier.Identifier.Text);
            }
        }
    }

    /// <summary>
    /// Generates code for a simple string result
    /// </summary>
    private string GenerateSimpleStringCode(string text)
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
            return ""{EscapeString(text)}"";
        }}
    }}
}}";
    }

    /// <summary>
    /// Generates code for an empty string result
    /// </summary>
    private string GenerateEmptyStringCode()
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
    /// Compiles source code into an assembly
    /// </summary>
    private async Task<Assembly> CompileSourceCodeAsync(string sourceCode)
    {
        return await Task.Run(() => {
            // Parse the source code
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

            // Set compilation options
            var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithOptimizationLevel(OptimizationLevel.Release);

            // Get required references
            var references = GetRequiredReferences();

            // Create unique assembly name
            string assemblyName = $"DynamicAssembly_{Guid.NewGuid():N}";

            // Create the compilation
            var compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { syntaxTree },
                references,
                options);

            // Emit to memory stream
            using var ms = new MemoryStream();
            var result = compilation.Emit(ms);

            // Handle compilation errors
            if (!result.Success)
            {
                var errors = string.Join(Environment.NewLine,
                    result.Diagnostics
                        .Where(d => d.IsWarningAsError || d.Severity == DiagnosticSeverity.Error)
                        .Select(d => $"{d.Id}: {d.GetMessage()}"));

                throw new Exception($"Compilation errors:{Environment.NewLine}{errors}");
            }

            // Load the assembly
            ms.Seek(0, SeekOrigin.Begin);
            return Assembly.Load(ms.ToArray());
        });
    }

    /// <summary>
    /// Gets required references for compilation
    /// </summary>
    private List<MetadataReference> GetRequiredReferences()
    {
        var references = new List<MetadataReference>
        {
            // Core .NET types
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(FormattableString).Assembly.Location),
            
            // System assemblies
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location),
            
            // Collections and LINQ
            MetadataReference.CreateFromFile(typeof(System.Collections.IEnumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
            
            // Text and Globalization
            MetadataReference.CreateFromFile(typeof(StringBuilder).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Globalization.CultureInfo).Assembly.Location)
        };

        // Try to add Microsoft.CSharp for dynamic features
        try
        {
            var csharpAssembly = Assembly.Load("Microsoft.CSharp");
            if (csharpAssembly != null)
            {
                references.Add(MetadataReference.CreateFromFile(csharpAssembly.Location));
            }
        }
        catch
        {
            // Ignore if not available
        }

        return references;
    }

    /// <summary>
    /// Tries to get a compiled expression from cache
    /// </summary>
    private bool TryGetFromCache(string key, out CompiledExpression expression)
    {
        lock (_cacheLock)
        {
            return _cache.TryGetValue(key, out expression!);
        }
    }

    /// <summary>
    /// Adds a compiled expression to the cache
    /// </summary>
    private void AddToCache(string key, CompiledExpression expression)
    {
        lock (_cacheLock)
        {
            _cache[key] = expression;
        }
    }

    /// <summary>
    /// Clears the compilation cache
    /// </summary>
    internal void ClearCache()
    {
        lock (_cacheLock)
        {
            _cache.Clear();
        }
    }
}