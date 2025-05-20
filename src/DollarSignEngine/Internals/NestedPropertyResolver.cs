namespace DollarSignEngine.Internals;

/// <summary>
/// Utility class for resolving nested property paths from objects
/// </summary>
internal static class NestedPropertyResolver
{
    /// <summary>
    /// Resolves a property value from a nested path.
    /// Example: "Company.Address.City" from an object with nested properties.
    /// </summary>
    public static object? ResolveNestedProperty(object? source, string path)
    {
        if (source == null || string.IsNullOrEmpty(path)) return null;
        object? current = source;
        string[] parts = path.Split('.');

        foreach (var part in parts)
        {
            if (current == null) return null;

            // Handle DictionaryWrapper
            if (current is DictionaryWrapper dw)
            {
                current = dw.TryGetValue(part);
                continue;
            }

            // Handle generic dictionary with string keys - case sensitive
            if (current is IDictionary<string, object> genericDict)
            {
                if (genericDict.TryGetValue(part, out var dictValue))
                {
                    current = dictValue;
                    continue;
                }

                // Try case-insensitive lookup
                var key = genericDict.Keys.FirstOrDefault(k => string.Equals(k, part, StringComparison.OrdinalIgnoreCase));
                if (key != null && genericDict.TryGetValue(key, out dictValue))
                {
                    current = dictValue;
                    continue;
                }

                return null;
            }

            // Handle generic dictionary with possibly null values - case sensitive
            if (current is IDictionary<string, object?> genericDictNullable)
            {
                if (genericDictNullable.TryGetValue(part, out var dictValue))
                {
                    current = dictValue;
                    continue;
                }

                // Try case-insensitive lookup
                var key = genericDictNullable.Keys.FirstOrDefault(k => string.Equals(k, part, StringComparison.OrdinalIgnoreCase));
                if (key != null && genericDictNullable.TryGetValue(key, out dictValue))
                {
                    current = dictValue;
                    continue;
                }

                return null;
            }

            // Handle non-generic dictionary
            if (current is IDictionary nonGenericDict)
            {
                object? tempKey = part;
                if (!nonGenericDict.Contains(tempKey))
                {
                    // Try case-insensitive lookup
                    tempKey = nonGenericDict.Keys.Cast<object>()
                        .FirstOrDefault(k => k is string ks && string.Equals(ks, part, StringComparison.OrdinalIgnoreCase));
                }

                if (tempKey != null && nonGenericDict.Contains(tempKey))
                {
                    current = nonGenericDict[tempKey];
                    continue;
                }

                return null;
            }

            // Try to get property via reflection with case-insensitive comparison
            current = DataPreparationHelper.ResolvePropertyValueFromObject(current, part);
            if (current == null) return null;
        }

        return current;
    }
}