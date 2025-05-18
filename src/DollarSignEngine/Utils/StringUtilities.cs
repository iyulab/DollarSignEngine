using System.Text.RegularExpressions;

namespace DollarSignEngine.Utils;

/// <summary>
/// String utilities for code generation
/// </summary>
internal static class StringUtilities
{
    /// <summary>
    /// Sanitizes a variable name for use in generated code
    /// </summary>
    internal static string SanitizeVariableName(string name)
    {
        // Ensure variable names don't conflict with C# keywords
        // and are valid identifiers
        return $"var_{name.Replace(".", "_")}";
    }

    /// <summary>
    /// Escapes special characters in a string for C# code generation
    /// </summary>
    internal static string EscapeString(string text)
    {
        return text
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }

    /// <summary>
    /// Replaces a whole word in a string, preserving property access
    /// </summary>
    internal static string ReplaceWholeWord(string text, string oldWord, string newWord)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(oldWord))
            return text;

        return Regex.Replace(
            text,
            $@"\b{Regex.Escape(oldWord)}\b",
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
}