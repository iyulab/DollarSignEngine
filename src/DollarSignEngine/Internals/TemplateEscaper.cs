namespace DollarSignEngine.Internals;

/// <summary>
/// Provides functionality to escape and unescape template delimiters (double braces).
/// This utility class is used for processing template strings before and after applying template operations.
/// </summary>
internal static class TemplateEscaper
{
    // Define escape tokens as readonly constants
    private static readonly string OPEN = "@@OPEN@@";
    private static readonly string CLOSE = "@@CLOSE@@";

    /// <summary>
    /// Escapes double braces in a template string to prevent them from being interpreted as special characters
    /// during template processing. Uses a two-pass algorithm to ensure proper handling of nested braces.
    /// </summary>
    public static string EscapeBlocks(string template)
    {
        if (string.IsNullOrEmpty(template))
        {
            return template;
        }

        // IMPORTANT: The two-pass approach is critical for handling nested braces correctly
        // First pass: Replace "{{" with "@@OPEN@@" (front to back)
        System.Text.StringBuilder pass1Builder = new System.Text.StringBuilder(template.Length * 2);
        int i = 0;
        while (i < template.Length)
        {
            if (i <= template.Length - 2 && template[i] == '{' && template[i + 1] == '{')
            {
                pass1Builder.Append(OPEN);
                i += 2; // Skip the "{{" pair
            }
            else
            {
                pass1Builder.Append(template[i]);
                i++;
            }
        }

        string intermediateResult = pass1Builder.ToString();

        // Second pass: Replace "}}" with "@@CLOSE@@" (back to front)
        // This backward scan is essential for handling nested templates correctly
        System.Text.StringBuilder finalBuilder = new System.Text.StringBuilder(intermediateResult.Length * 2);
        int j = intermediateResult.Length - 1;

        while (j >= 0)
        {
            if (j > 0 && intermediateResult[j - 1] == '}' && intermediateResult[j] == '}')
            {
                finalBuilder.Insert(0, CLOSE); // Insert at the beginning
                j -= 2; // Skip the "}}" pair
            }
            else
            {
                finalBuilder.Insert(0, intermediateResult[j]); // Insert at the beginning
                j--;
            }
        }

        return finalBuilder.ToString();
    }

    /// <summary>
    /// Unescapes previously escaped template delimiters back to their original form.
    /// </summary>
    public static string UnescapeBlocks(string template)
    {
        if (string.IsNullOrEmpty(template))
        {
            return template;
        }

        var result = new System.Text.StringBuilder(template.Length);
        int i = 0;

        while (i < template.Length)
        {
            // Fast path check for OPEN token
            if (i <= template.Length - OPEN.Length &&
                template[i] == '@' && template[i + 1] == '@' &&
                MatchesAt(template, i, OPEN))
            {
                result.Append('{');
                i += OPEN.Length;
            }
            // Fast path check for CLOSE token
            else if (i <= template.Length - CLOSE.Length &&
                     template[i] == '@' && template[i + 1] == '@' &&
                     MatchesAt(template, i, CLOSE))
            {
                result.Append('}');
                i += CLOSE.Length;
            }
            else
            {
                result.Append(template[i]);
                i++;
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Helper method to efficiently check if a substring matches at a specific position.
    /// This avoids creating unnecessary string objects when comparing substrings.
    /// </summary>
    private static bool MatchesAt(string source, int startIndex, string target)
    {
        if (startIndex + target.Length > source.Length)
        {
            return false;
        }

        for (int i = 0; i < target.Length; i++)
        {
            if (source[startIndex + i] != target[i])
            {
                return false;
            }
        }

        return true;
    }
}