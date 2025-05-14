namespace DollarSignEngine.Parsing;

/// <summary>
/// Handles parsing of string templates with interpolated expressions.
/// </summary>
internal class TemplateParser
{
    // Unique mask pattern (very unlikely to appear in real strings)
    private const string MASK_PREFIX = "__$$_";
    private const string MASK_SUFFIX = "_$$__";

    /// <summary>
    /// Parses a template string and extracts interpolated expressions.
    /// </summary>
    public (string Processed, List<InterpolationPart> Parts) Parse(string template, DollarSignOptions option)
    {
        // Handle escaped braces first
        template = HandleEscapedBraces(template);

        // Extract interpolated expressions based on the syntax mode
        List<InterpolationPart> interpolationParts = new();
        string processed = template;

        if (option.SupportDollarSignSyntax)
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

        // First, protect ${...} patterns 
        string processed = ProtectDollarSignBraces(template, protectedDollarSignBraces);

        // Extract standard {...} patterns - using balanced brace scanning for better accuracy
        var braceExpressions = ExtractBalancedBraceExpressions(processed);

        int indexOffset = 0;
        foreach (var (expression, startIndex, endIndex) in braceExpressions)
        {
            string originalExpression = expression;
            string placeholder = $"{MASK_PREFIX}{parts.Count}{MASK_SUFFIX}";

            // Create interpolation part
            parts.Add(new InterpolationPart
            {
                OriginalText = "{" + originalExpression + "}",
                Expression = originalExpression,
                StartIndex = startIndex,
                EndIndex = endIndex,
                Placeholder = placeholder
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
    /// Extracts expressions surrounded by balanced braces.
    /// </summary>
    private List<(string Expression, int StartIndex, int EndIndex)> ExtractBalancedBraceExpressions(string text)
    {
        List<(string, int, int)> results = new();
        int depth = 0;
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

            // Track brace nesting
            if (current == '{')
            {
                depth++;
                // If this is the first opening brace, mark the start
                if (depth == 1)
                {
                    startIndex = i;
                }
            }
            else if (current == '}')
            {
                if (depth > 0)
                {
                    depth--;
                    // If we've closed the outermost brace, extract the expression
                    if (depth == 0 && startIndex.HasValue)
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
    /// Extracts dollar sign interpolation expressions ${expression} from the template.
    /// </summary>
    private string ExtractDollarSignInterpolations(string template, List<InterpolationPart> parts)
    {
        // Extract ${...} patterns using balanced brace scanning
        var dollarBraceExpressions = ExtractBalancedDollarBraceExpressions(template);

        string processed = template;
        int indexOffset = 0;
        foreach (var (expression, startIndex, endIndex) in dollarBraceExpressions)
        {
            string originalExpression = expression;
            string placeholder = $"{MASK_PREFIX}{parts.Count}{MASK_SUFFIX}";

            // Create interpolation part
            parts.Add(new InterpolationPart
            {
                OriginalText = "${" + originalExpression + "}",
                Expression = originalExpression,
                StartIndex = startIndex,
                EndIndex = endIndex,
                Placeholder = placeholder
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
    /// Extracts expressions surrounded by balanced dollar braces.
    /// </summary>
    private List<(string Expression, int StartIndex, int EndIndex)> ExtractBalancedDollarBraceExpressions(string text)
    {
        List<(string, int, int)> results = new();
        bool inString = false;
        char stringDelimiter = '\0';

        for (int i = 0; i < text.Length - 1; i++)
        {
            // Look for ${ pattern
            if (text[i] == '$' && text[i + 1] == '{' && (!inString))
            {
                int start = i;
                int braceDepth = 1;
                i += 2; // Skip the ${

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

                    // Track brace nesting
                    if (current == '{')
                    {
                        braceDepth++;
                    }
                    else if (current == '}')
                    {
                        braceDepth--;
                        if (braceDepth == 0)
                        {
                            // Extract the expression without the ${ and }
                            string expression = text.Substring(start + 2, i - start - 2);
                            results.Add((expression, start, i));
                            break;
                        }
                    }
                    i++;
                }
            }
            else if ((text[i] == '"' || text[i] == '\'') && (i == 0 || text[i - 1] != '\\'))
            {
                // Toggle string state outside of main scanning
                if (!inString)
                {
                    inString = true;
                    stringDelimiter = text[i];
                }
                else if (text[i] == stringDelimiter)
                {
                    inString = false;
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Protects ${...} patterns in a string.
    /// </summary>
    private string ProtectDollarSignBraces(string text, List<(string, string, int)> protectedPatterns)
    {
        List<(string, int, int)> dollarExpressions = ExtractBalancedDollarBraceExpressions(text);

        // Sort by start index in descending order to avoid index changes
        dollarExpressions.Sort((a, b) => b.Item2.CompareTo(a.Item2));

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
    public string RestoreEscapedBraces(string text)
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