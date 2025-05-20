namespace DollarSignEngine.Internals;

/// <summary>
/// Utility class for resolving nested property paths from objects.
/// </summary>
internal static class PropertyResolver
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

            // Handle specific object types
            if (TryGetValueFromSpecificTypes(current, part, out var result))
            {
                current = result;
                continue;
            }

            // Use TypeAccessor for standard property access
            var accessor = TypeAccessorFactory.GetTypeAccessor(current.GetType());
            if (!accessor.TryGetPropertyValue(current, part, out current))
            {
                return null;
            }
        }

        return current;
    }

    /// <summary>
    /// Tries to get a value from a specific object type like dictionaries or DictionaryWrapper.
    /// </summary>
    private static bool TryGetValueFromSpecificTypes(object source, string key, out object? value)
    {
        // Handle DictionaryWrapper
        if (source is DictionaryWrapper dw)
        {
            value = dw.TryGetValue(key);
            return true;
        }

        // Handle generic dictionaries with string keys - case sensitive and case insensitive
        if (source is IDictionary<string, object> genericDict)
        {
            if (genericDict.TryGetValue(key, out value))
            {
                return true;
            }

            // Try case-insensitive lookup
            var matchingKey = genericDict.Keys.FirstOrDefault(k =>
                string.Equals(k, key, StringComparison.OrdinalIgnoreCase));

            if (matchingKey != null && genericDict.TryGetValue(matchingKey, out value))
            {
                return true;
            }

            value = null;
            return true;
        }

        // Handle generic dictionaries with nullable values
        if (source is IDictionary<string, object?> genericDictNullable)
        {
            if (genericDictNullable.TryGetValue(key, out value))
            {
                return true;
            }

            // Try case-insensitive lookup
            var matchingKey = genericDictNullable.Keys.FirstOrDefault(k =>
                string.Equals(k, key, StringComparison.OrdinalIgnoreCase));

            if (matchingKey != null && genericDictNullable.TryGetValue(matchingKey, out value))
            {
                return true;
            }

            value = null;
            return true;
        }

        // Handle non-generic dictionary (IDictionary)
        if (source is IDictionary nonGenericDict)
        {
            object? tempKey = key;
            if (!nonGenericDict.Contains(tempKey))
            {
                // Try case-insensitive lookup
                tempKey = nonGenericDict.Keys.Cast<object>()
                    .FirstOrDefault(k => k is string ks &&
                                        string.Equals(ks, key, StringComparison.OrdinalIgnoreCase));
            }

            if (tempKey != null && nonGenericDict.Contains(tempKey))
            {
                value = nonGenericDict[tempKey];
                return true;
            }

            value = null;
            return true;
        }

        // Not handled
        value = null;
        return false;
    }
}