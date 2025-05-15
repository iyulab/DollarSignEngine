namespace DollarSignEngine.Parsing;

/// <summary>
/// Parses format specifier and alignment from an expression.
/// </summary>
internal class TemplateParser
{
    // Constants for placeholder generation
    private const string MASK_PREFIX = "__$$_";
    private const string MASK_SUFFIX = "_$$__";

    // Regex patterns for format specifiers and alignment
    private static readonly Regex FormatSpecifierRegex = new(@"^(.+?)(?:,\s*(-?\d+))?(?::\s*(.+))?$", RegexOptions.Compiled);

    /// <summary>
    /// Parses a template string and extracts interpolated expressions.
    /// </summary>
    internal (string Processed, List<InterpolationPart> Parts) Parse(string template, DollarSignOptions options)
    {
        // Handle escaped braces first
        template = HandleEscapedBraces(template);

        // Extract interpolated expressions based on the syntax mode
        List<InterpolationPart> interpolationParts = new();
        string processed = template;

        if (options.SupportDollarSignSyntax)
        {
            // Dollar sign mode: process ${...} and leave {...} as literals
            processed = ExtractDollarSignInterpolations(processed, interpolationParts);
        }
        else
        {
            // Standard mode: process {...} and leave ${...} as literals
            processed = ExtractStandardInterpolations(processed, interpolationParts);
        }

        return (processed, interpolationParts);
    }

    /// <summary>
    /// Extracts standard interpolation expressions {expression} from the template.
    /// </summary>
    private string ExtractStandardInterpolations(string template, List<InterpolationPart> parts)
    {
        List<(string original, string masked, int index)> protectedDollarSignBraces = new();

        // Protect ${...} patterns first
        string processed = ProtectDollarSignBraces(template, protectedDollarSignBraces);

        // Extract all bracketed expressions first with awareness of nested brackets
        var braceExpressions = ExtractBracketedExpressions(processed, '{', '}');

        int indexOffset = 0;
        foreach (var (fullExpression, startIndex, endIndex) in braceExpressions)
        {
            string placeholder = $"{MASK_PREFIX}{parts.Count}{MASK_SUFFIX}";

            // Parse expression and format specifier with awareness of ternary expressions
            var parsedExpression = ParseExpressionAndFormat(fullExpression);
            string expression = parsedExpression.Expression;
            int? alignment = parsedExpression.Alignment;
            string? formatSpecifier = parsedExpression.FormatSpecifier;

            // Create interpolation part
            parts.Add(new InterpolationPart
            {
                OriginalText = "{" + fullExpression + "}",
                Expression = expression,
                StartIndex = startIndex,
                EndIndex = endIndex,
                Placeholder = placeholder,
                Alignment = alignment,
                FormatSpecifier = formatSpecifier
            });

            // Replace the expression with a placeholder
            int startPosition = startIndex - indexOffset;
            int length = endIndex - startIndex + 1;
            processed = processed.Remove(startPosition, length);
            processed = processed.Insert(startPosition, placeholder);

            // Adjust offset for future replacements
            indexOffset += length - placeholder.Length;
        }

        // Restore protected ${...} patterns
        foreach (var (original, masked, _) in protectedDollarSignBraces)
        {
            processed = processed.Replace(masked, original);
        }

        return processed;
    }

    /// <summary>
    /// Extracts dollar sign interpolation expressions ${expression} from the template.
    /// </summary>
    private string ExtractDollarSignInterpolations(string template, List<InterpolationPart> parts)
    {
        // Extract all expressions that start with ${ and end with a balanced }
        var dollarBraceExpressions = ExtractDollarBraceExpressions(template);

        string processed = template;
        int indexOffset = 0;
        foreach (var (fullExpression, startIndex, endIndex) in dollarBraceExpressions)
        {
            string placeholder = $"{MASK_PREFIX}{parts.Count}{MASK_SUFFIX}";

            // Parse expression and format specifier with awareness of ternary expressions
            var parsedExpression = ParseExpressionAndFormat(fullExpression);
            string expression = parsedExpression.Expression;
            int? alignment = parsedExpression.Alignment;
            string? formatSpecifier = parsedExpression.FormatSpecifier;

            // Create interpolation part
            parts.Add(new InterpolationPart
            {
                OriginalText = "${" + fullExpression + "}",
                Expression = expression,
                StartIndex = startIndex,
                EndIndex = endIndex,
                Placeholder = placeholder,
                Alignment = alignment,
                FormatSpecifier = formatSpecifier
            });

            // Replace the expression with a placeholder
            int startPosition = startIndex - indexOffset;
            int length = endIndex - startIndex + 1;
            processed = processed.Remove(startPosition, length);
            processed = processed.Insert(startPosition, placeholder);

            // Adjust offset for future replacements
            indexOffset += length - placeholder.Length;
        }

        return processed;
    }

    /// <summary>
    /// Extracts expressions surrounded by the given delimiters, properly handling nesting and string literals.
    /// </summary>
    private List<(string Expression, int StartIndex, int EndIndex)> ExtractBracketedExpressions(string text, char openBracket, char closeBracket)
    {
        List<(string, int, int)> results = new();
        int bracketDepth = 0;
        int? startIndex = null;
        bool inString = false;
        char stringDelimiter = '\0';

        for (int i = 0; i < text.Length; i++)
        {
            char current = text[i];

            // Handle string literals
            if ((current == '"' || current == '\'') && (i == 0 || text[i - 1] != '\\'))
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

            // Track bracket nesting
            if (current == openBracket)
            {
                bracketDepth++;
                // If this is the first opening bracket, mark the start
                if (bracketDepth == 1)
                {
                    startIndex = i;
                }
            }
            else if (current == closeBracket)
            {
                if (bracketDepth > 0)
                {
                    bracketDepth--;
                    // If we've closed the outermost bracket, extract the expression
                    if (bracketDepth == 0 && startIndex.HasValue)
                    {
                        string expression = text.Substring(startIndex.Value + 1, i - startIndex.Value - 1);
                        results.Add((expression, startIndex.Value, i));
                        startIndex = null;
                    }
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Extracts dollar brace expressions like ${...} from text.
    /// </summary>
    private List<(string Expression, int StartIndex, int EndIndex)> ExtractDollarBraceExpressions(string text)
    {
        List<(string, int, int)> results = new();

        for (int i = 0; i < text.Length - 1; i++)
        {
            // Look for ${ pattern that's not in a string
            if (text[i] == '$' && text[i + 1] == '{')
            {
                bool inString = false;
                char stringDelimiter = '\0';
                int bracketDepth = 1;
                int startIndex = i;
                i += 2; // Skip past the ${

                int expressionStart = i;

                while (i < text.Length)
                {
                    char current = text[i];

                    // Handle string literals
                    if ((current == '"' || current == '\'') && (i == 0 || text[i - 1] != '\\'))
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
                        i++;
                        continue;
                    }

                    // Skip characters inside string literals
                    if (inString)
                    {
                        i++;
                        continue;
                    }

                    // Track bracket nesting
                    if (current == '{')
                    {
                        bracketDepth++;
                    }
                    else if (current == '}')
                    {
                        bracketDepth--;
                        if (bracketDepth == 0)
                        {
                            // Extract the expression
                            string expression = text.Substring(expressionStart, i - expressionStart);
                            results.Add((expression, startIndex, i));
                            break;
                        }
                    }

                    i++;
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Parses an expression to separate the expression, alignment, and format specifier,
    /// properly handling ternary operators.
    /// </summary>
    private (string Expression, int? Alignment, string? FormatSpecifier) ParseExpressionAndFormat(string input)
    {
        // Primary colon detection for format specifier vs ternary operator
        int colonIndex = FindFormatSpecifierColon(input);

        if (colonIndex > 0)
        {
            // Potential format specifier found
            string mainExpression = input.Substring(0, colonIndex).Trim();
            string formatPart = input.Substring(colonIndex + 1).Trim();

            // Check for alignment inside the expression before format
            int commaIndex = FindAlignmentComma(mainExpression);
            if (commaIndex > 0)
            {
                string expression = mainExpression.Substring(0, commaIndex).Trim();
                string alignmentStr = mainExpression.Substring(commaIndex + 1).Trim();

                if (int.TryParse(alignmentStr, out int alignmentVal))
                {
                    return (expression, alignmentVal, formatPart);
                }
            }

            // No alignment found
            return (mainExpression, null, formatPart);
        }

        // Check for alignment without format
        int alignIndex = FindAlignmentComma(input);
        if (alignIndex > 0)
        {
            string expression = input.Substring(0, alignIndex).Trim();
            string alignmentStr = input.Substring(alignIndex + 1).Trim();

            if (int.TryParse(alignmentStr, out int alignmentVal))
            {
                return (expression, alignmentVal, null);
            }
        }

        // No format specifier or alignment
        return (input.Trim(), null, null);
    }

    /// <summary>
    /// Finds the colon that separates a format specifier, not one in a ternary expression.
    /// </summary>
    private int FindFormatSpecifierColon(string input)
    {
        bool inString = false;
        char stringDelimiter = '\0';
        int parenDepth = 0;
        int bracketDepth = 0;
        int braceDepth = 0;
        bool hasTernary = false;

        for (int i = 0; i < input.Length; i++)
        {
            char current = input[i];

            // String handling
            if ((current == '"' || current == '\'') && (i == 0 || input[i - 1] != '\\'))
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

            if (inString) continue;

            // Nesting depth tracking
            if (current == '(') parenDepth++;
            else if (current == ')') parenDepth--;
            else if (current == '[') bracketDepth++;
            else if (current == ']') bracketDepth--;
            else if (current == '{') braceDepth++;
            else if (current == '}') braceDepth--;
            // Question mark indicates a ternary operator
            else if (current == '?' && parenDepth == 0 && bracketDepth == 0 && braceDepth == 0)
            {
                // Skip null coalescing operator
                if (i + 1 < input.Length && input[i + 1] == '?')
                {
                    i++;
                    continue;
                }
                hasTernary = true;
            }
            // Colon detection
            else if (current == ':' && parenDepth == 0 && bracketDepth == 0 && braceDepth == 0)
            {
                if (hasTernary)
                {
                    // Skip colons that are part of ternary expressions
                    hasTernary = false; // Reset for potential nested ternaries
                    continue;
                }

                // This is a format specifier colon
                return i;
            }
        }

        return -1; // No format specifier colon found
    }

    /// <summary>
    /// Finds the comma that separates alignment specifier, not one in a method call or other contexts.
    /// </summary>
    private int FindAlignmentComma(string input)
    {
        bool inString = false;
        char stringDelimiter = '\0';
        int parenDepth = 0;
        int bracketDepth = 0;
        int braceDepth = 0;

        for (int i = 0; i < input.Length; i++)
        {
            char current = input[i];

            // String handling
            if ((current == '"' || current == '\'') && (i == 0 || input[i - 1] != '\\'))
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

            if (inString) continue;

            // Nesting depth tracking
            if (current == '(') parenDepth++;
            else if (current == ')') parenDepth--;
            else if (current == '[') bracketDepth++;
            else if (current == ']') bracketDepth--;
            else if (current == '{') braceDepth++;
            else if (current == '}') braceDepth--;
            // Comma detection
            else if (current == ',' && parenDepth == 0 && bracketDepth == 0 && braceDepth == 0)
            {
                // This is likely an alignment separator
                return i;
            }
        }

        return -1; // No alignment comma found
    }

    /// <summary>
    /// Protects ${...} patterns in a string.
    /// </summary>
    private string ProtectDollarSignBraces(string text, List<(string, string, int)> protectedPatterns)
    {
        var dollarExpressions = ExtractDollarBraceExpressions(text);

        // Sort by start index in descending order to avoid index changes
        dollarExpressions.Sort((a, b) => b.StartIndex.CompareTo(a.StartIndex));

        string processed = text;
        int maskCount = 0;

        // Process from end to avoid index changes
        foreach (var (expression, startIndex, endIndex) in dollarExpressions)
        {
            // Construct the original pattern
            string original = "${" + expression + "}";

            // Create unique mask
            string masked = $"__DOLLAR_{maskCount++}__";

            // Store original string, mask, and position
            protectedPatterns.Add((original, masked, startIndex));

            // Replace at exact position
            int length = endIndex - startIndex + 1;
            processed = processed.Substring(0, startIndex) +
                     masked +
                     processed.Substring(startIndex + length);
        }

        return processed;
    }

    /// <summary>
    /// Handles escaped braces in the template.
    /// </summary>
    private string HandleEscapedBraces(string template)
    {
        // Replace {{ with a temporary placeholder
        template = template.Replace("{{", "~~OPEN_BRACE~~");
        // Replace }} with a temporary placeholder
        template = template.Replace("}}", "~~CLOSE_BRACE~~");
        return template;
    }

    /// <summary>
    /// Restores escaped braces from temporary placeholders.
    /// </summary>
    internal string RestoreEscapedBraces(string text)
    {
        // Restore {{ from temporary placeholder
        text = text.Replace("~~OPEN_BRACE~~", "{");
        // Restore }} from temporary placeholder
        text = text.Replace("~~CLOSE_BRACE~~", "}");
        return text;
    }
}

/// <summary>
/// Represents an interpolated expression part in a template.
/// </summary>
internal class InterpolationPart
{
    /// <summary>
    /// Gets or sets the original text of the interpolation, e.g. {expression:format}
    /// </summary>
    public string OriginalText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the expression to evaluate.
    /// </summary>
    public string Expression { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the start index of the interpolation in the original template.
    /// </summary>
    public int StartIndex { get; set; }

    /// <summary>
    /// Gets or sets the end index of the interpolation in the original template.
    /// </summary>
    public int EndIndex { get; set; }

    /// <summary>
    /// Gets or sets the placeholder that replaces the interpolation in the processed template.
    /// </summary>
    public string Placeholder { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the format specifier, if any.
    /// </summary>
    public string? FormatSpecifier { get; set; }

    /// <summary>
    /// Gets or sets the alignment, if any.
    /// </summary>
    public int? Alignment { get; set; }
}