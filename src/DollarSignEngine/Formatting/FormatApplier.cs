namespace DollarSignEngine.Formatting;

/// <summary>
/// Applies formatting to expression evaluation results.
/// </summary>
internal class FormatApplier
{
    /// <summary>
    /// Formats a value according to the format specifier and alignment.
    /// </summary>
    public string Format(object? value, string? formatSpecifier, int? alignment, CultureInfo? culture, DollarSignOptions option)
    {
        if (value == null)
        {
            return string.Empty;
        }

        // Apply format if provided
        string result;
        culture ??= CultureInfo.CurrentCulture;

        if (!string.IsNullOrEmpty(formatSpecifier) && value is IFormattable formattable)
        {
            try
            {
                result = formattable.ToString(formatSpecifier, culture);
                Log.Debug($"Applied format specifier '{formatSpecifier}' to value '{value}', result: '{result}'", option);
            }
            catch (FormatException ex)
            {
                Log.Debug($"Format error: {ex.Message}", option);
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
                Log.Debug($"Applied right alignment {alignment.Value} to '{result}'", option);
            }
            else if (alignment.Value < 0)
            {
                result = result.PadRight(spaces);
                Log.Debug($"Applied left alignment {alignment.Value} to '{result}'", option);
            }
        }

        return result;
    }
}