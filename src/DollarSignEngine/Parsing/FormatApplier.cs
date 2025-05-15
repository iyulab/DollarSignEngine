using System;
using System.Globalization;
using DollarSignEngine.Utilities;

namespace DollarSignEngine.Parsing;

/// <summary>
/// Applies formatting to expression evaluation results.
/// </summary>
internal class FormatApplier
{
    /// <summary>
    /// Formats a value according to the format specifier and alignment.
    /// </summary>
    public string Format(object? value, string? formatSpecifier, int? alignment, CultureInfo? culture, DollarSignOptions options)
    {
        if (value == null)
        {
            return string.Empty;
        }

        // Use current culture if not specified
        culture ??= CultureInfo.CurrentCulture;

        // Apply format if provided
        string result;
        if (!string.IsNullOrEmpty(formatSpecifier) && value is IFormattable formattable)
        {
            try
            {
                result = formattable.ToString(formatSpecifier, culture);
                Log.Debug($"Applied format specifier '{formatSpecifier}' to value '{value}', result: '{result}'", options);
            }
            catch (FormatException ex)
            {
                Log.Debug($"Format error: {ex.Message}", options);
                result = Convert.ToString(value, culture) ?? string.Empty;
            }
        }
        else
        {
            result = Convert.ToString(value, culture) ?? string.Empty;
        }

        // Apply alignment if provided
        if (alignment.HasValue)
        {
            int spaces = Math.Abs(alignment.Value);
            if (alignment.Value > 0)
            {
                result = result.PadLeft(spaces);
                Log.Debug($"Applied right alignment {alignment.Value} to '{result}'", options);
            }
            else if (alignment.Value < 0)
            {
                result = result.PadRight(spaces);
                Log.Debug($"Applied left alignment {alignment.Value} to '{result}'", options);
            }
        }

        return result;
    }
}