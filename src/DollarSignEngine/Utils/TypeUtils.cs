namespace DollarSignEngine.Utils;

/// <summary>
/// Utilities for type handling and conversion
/// </summary>
internal static class TypeUtils
{
    /// <summary>
    /// Gets the appropriate C# type name to use in code generation
    /// </summary>
    public static string GetAppropriateTypeName(object? value)
    {
        if (value == null)
            return "object";

        Type type = value.GetType();

        // Handle common primitive types
        if (type == typeof(string))
            return "string";
        if (type == typeof(int))
            return "int";
        if (type == typeof(long))
            return "long";
        if (type == typeof(double))
            return "double";
        if (type == typeof(decimal))
            return "decimal";
        if (type == typeof(bool))
            return "bool";
        if (type == typeof(DateTime))
            return "DateTime";

        // For other types, use the fully qualified name
        return "object";
    }

    /// <summary>
    /// Generates appropriate cast expression if needed
    /// </summary>
    public static string GetCastExpression(string variableName, string targetType)
    {
        if (targetType == "object")
            return variableName;

        return $"({targetType})({variableName})";
    }

    /// <summary>
    /// Wraps a variable name with appropriate null-handling conversion
    /// </summary>
    public static string GenerateSafeTypedAccess(string variableName, string targetType)
    {
        if (targetType == "string")
            return $"{variableName}?.ToString() ?? string.Empty";

        if (targetType == "bool")
            return $"Convert.ToBoolean({variableName})";

        if (targetType == "int")
            return $"Convert.ToInt32({variableName})";

        if (targetType == "long")
            return $"Convert.ToInt64({variableName})";

        if (targetType == "double")
            return $"Convert.ToDouble({variableName})";

        if (targetType == "decimal")
            return $"Convert.ToDecimal({variableName})";

        if (targetType == "DateTime")
            return $"Convert.ToDateTime({variableName})";

        // Fall back to cast for other types
        return $"({variableName} is {targetType} typed{variableName} ? typed{variableName} : default({targetType}))";
    }

    /// <summary>
    /// Converts a value to a specific type, handling nulls
    /// </summary>
    public static object? ConvertValueToType(object? value, Type targetType)
    {
        if (value == null)
        {
            // Handle null values for different target types
            if (targetType == typeof(string))
                return string.Empty;

            if (targetType.IsValueType)
                return Activator.CreateInstance(targetType); // Default value for value types

            return null;
        }

        // Already the correct type
        if (value.GetType() == targetType)
            return value;

        // Try to convert
        try
        {
            return Convert.ChangeType(value, targetType);
        }
        catch
        {
            // If conversion fails, return appropriate default
            if (targetType == typeof(string))
                return value.ToString();

            if (targetType.IsValueType)
                return Activator.CreateInstance(targetType);

            return null;
        }
    }
}