using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;
using System.Text;

namespace DollarSignEngine.Internals;

/// <summary>
/// Helper class for extracting and parsing interpolation expressions
/// </summary>
internal static class InterpolationParser
{
    /// <summary>
    /// Extracts all variables from an expression
    /// </summary>
    internal static void ExtractVariables(ExpressionSyntax expression, HashSet<string> variables)
    {
        // Process simple identifiers
        if (expression is IdentifierNameSyntax identifier)
        {
            variables.Add(identifier.Identifier.Text);
            return;
        }

        // Process member access expressions (e.g., person.Name)
        if (expression is MemberAccessExpressionSyntax memberAccess)
        {
            string path = ExtractPropertyPath(memberAccess);
            if (!string.IsNullOrEmpty(path))
            {
                variables.Add(path);

                // Also add the root object name in case it's used elsewhere
                if (memberAccess.Expression is IdentifierNameSyntax rootIdentifier)
                {
                    variables.Add(rootIdentifier.Identifier.Text);
                }
            }
            return;
        }

        // For all other expressions, find all contained identifiers
        foreach (var node in expression.DescendantNodesAndSelf())
        {
            if (node is IdentifierNameSyntax childId && node != expression)
            {
                variables.Add(childId.Identifier.Text);
            }
            else if (node is MemberAccessExpressionSyntax childMemberAccess && node != expression)
            {
                string path = ExtractPropertyPath(childMemberAccess);
                if (!string.IsNullOrEmpty(path))
                {
                    variables.Add(path);

                    // Also add the root object name in case it's used elsewhere
                    if (childMemberAccess.Expression is IdentifierNameSyntax rootIdentifier)
                    {
                        variables.Add(rootIdentifier.Identifier.Text);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Extracts the full property path from a member access expression
    /// </summary>
    internal static string ExtractPropertyPath(MemberAccessExpressionSyntax memberAccess)
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
}

/// <summary>
/// Class for parsing string interpolation expressions with special characters
/// </summary>
internal class StringInterpolationParser
{
    /// <summary>
    /// Represents a part of an interpolation string
    /// </summary>
    internal class InterpolationPart
    {
        public string Content { get; }
        public bool IsVariable { get; }
        public string? Alignment { get; }
        public string? Format { get; }

        public InterpolationPart(string content, bool isVariable, string? alignment = null, string? format = null)
        {
            Content = content;
            IsVariable = isVariable;
            Alignment = alignment;
            Format = format;
        }
    }

    /// <summary>
    /// Parses a string with interpolation expressions
    /// </summary>
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

/// <summary>
/// Helper for getting compilation references
/// </summary>
internal static class CompilationReferences
{
    /// <summary>
    /// Gets required references for compilation
    /// </summary>
    internal static List<MetadataReference> GetRequiredReferences()
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
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            
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
}