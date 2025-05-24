namespace DollarSignEngine.Internals;

/// <summary>
/// Helper methods for preparing and processing context data for evaluation.
/// </summary>
internal static class DataPreparationHelper
{
    /// <summary>
    /// Prepares the evaluation context by merging local and global data.
    /// </summary>
    public static object PrepareContext(object? variables, DollarSignOptions effectiveOptions)
    {
        IDictionary<string, Type>? globalVariableTypes = null;

        // Handle the effective variables based on local and global data
        var localVariables = variables ?? new DollarSign.NoParametersContext();
        var globalData = effectiveOptions.GlobalData;

        // Convert both to dictionaries for merging
        var localDict = ToDictionary(localVariables);
        var globalDict = ToDictionary(globalData);

        // Merge the dictionaries with local variables taking precedence
        var mergedDict = MergeDictionaries(globalDict, localDict);

        // Get combined property types
        globalVariableTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in mergedDict)
        {
            if (kvp.Value != null)
            {
                globalVariableTypes[kvp.Key] = kvp.Value.GetType();
            }
            else
            {
                globalVariableTypes[kvp.Key] = typeof(object);
            }
        }

        effectiveOptions.GlobalVariableTypes = globalVariableTypes;

        // Create a ScriptHost with the merged data
        return new ScriptHost(mergedDict);
    }

    /// <summary>
    /// Determines if a type is an anonymous type.
    /// </summary>
    public static bool IsAnonymousType(Type? type)
    {
        if (type == null) return false;
        return Attribute.IsDefined(type, typeof(CompilerGeneratedAttribute), false)
            && type.IsGenericType && type.Name.Contains("AnonymousType")
            && (type.Name.StartsWith("<>") || type.Name.StartsWith("VB$"))
            && (type.Attributes & TypeAttributes.NotPublic) == TypeAttributes.NotPublic;
    }

    /// <summary>
    /// Converts an object to a format more suitable for evaluation.
    /// </summary>
    public static object? ConvertToEvalFriendlyObject(object? obj)
    {
        if (obj == null) return null;
        Type type = obj.GetType();

        // Process anonymous types
        if (IsAnonymousType(type))
        {
            var expando = new ExpandoObject();
            var dict = (IDictionary<string, object?>)expando;
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.CanRead)
                {
                    dict[property.Name] = ConvertToEvalFriendlyObject(property.GetValue(obj));
                }
            }
            return expando;
        }

        // Process arrays
        if (type.IsArray)
        {
            var array = (Array)obj;
            Type? elementType = type.GetElementType();

            if (elementType == null) return obj;

            // For numeric arrays, convert to IEnumerable<T> or List<T> to ensure LINQ extension methods work
            if (elementType == typeof(int) ||
                elementType == typeof(long) ||
                elementType == typeof(float) ||
                elementType == typeof(double) ||
                elementType == typeof(decimal))
            {
                // Convert array to List<T> which has better LINQ support
                var listType = typeof(List<>).MakeGenericType(elementType);
                var list1 = Activator.CreateInstance(listType);
                var addMethod = listType.GetMethod("Add");

                if (addMethod != null)
                {
                    foreach (var item in array)
                    {
                        addMethod.Invoke(list1, new[] { item });
                    }
                    return list1;
                }
            }

            // For primitive types, keep as is
            if (elementType.IsPrimitive || elementType == typeof(string) || elementType == typeof(decimal) ||
                elementType == typeof(DateTime) || elementType == typeof(DateTimeOffset) ||
                (!IsAnonymousType(elementType) && !typeof(IEnumerable).IsAssignableFrom(elementType) || elementType == typeof(string)))
            {
                return obj;
            }

            // For complex elements, convert each element
            var items = new List<object?>(array.Length);
            foreach (var item in array)
            {
                items.Add(ConvertToEvalFriendlyObject(item));
            }
            return items.ToArray();
        }

        // Process dictionaries
        if (obj is IDictionary<string, object?> dictNullable)
        {
            var newDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in dictNullable)
            {
                newDict[kvp.Key] = ConvertToEvalFriendlyObject(kvp.Value);
            }
            return newDict;
        }

        if (obj is IDictionary<string, object> dictNonNullable)
        {
            var newDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in dictNonNullable)
            {
                newDict[kvp.Key] = ConvertToEvalFriendlyObject(kvp.Value);
            }
            return newDict;
        }

        // Process generic Dictionary types
        if (obj is IDictionary dictionary && type.IsGenericType)
        {
            var genericTypeDef = type.GetGenericTypeDefinition();
            if (genericTypeDef == typeof(Dictionary<,>) || genericTypeDef == typeof(IDictionary<,>))
            {
                var keyType = type.GetGenericArguments()[0];

                // Only process if keys can be converted to strings
                if (keyType == typeof(string) || keyType.IsPrimitive || keyType == typeof(Guid))
                {
                    var newDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        string key = entry.Key?.ToString() ?? string.Empty;
                        newDict[key] = ConvertToEvalFriendlyObject(entry.Value);
                    }
                    return newDict;
                }
            }
        }

        // Process any generic IDictionary
        if (obj is IDictionary nonGenericDict)
        {
            var newDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in nonGenericDict)
            {
                string key = entry.Key?.ToString() ?? string.Empty;
                newDict[key] = ConvertToEvalFriendlyObject(entry.Value);
            }
            return newDict;
        }

        // Process lists of anonymous types or dictionaries
        if (obj is IList list && type.IsGenericType)
        {
            Type listType = type.GetGenericTypeDefinition();
            if (listType == typeof(List<>) || listType == typeof(IList<>))
            {
                Type elementType = type.GetGenericArguments()[0];

                // If the element type is anonymous or a dictionary, convert each element
                if (IsAnonymousType(elementType) ||
                    typeof(IDictionary).IsAssignableFrom(elementType) ||
                    elementType == typeof(object))
                {
                    var newList = new List<object?>(list.Count);
                    foreach (var item in list)
                    {
                        newList.Add(ConvertToEvalFriendlyObject(item));
                    }
                    return newList;
                }
            }
        }

        // Process other enumerable collections (that are not strings)
        if (obj is IEnumerable enumerable && !(obj is string))
        {
            var items = new List<object?>();
            foreach (var item in enumerable)
            {
                items.Add(ConvertToEvalFriendlyObject(item));
            }
            return items;
        }

        // For other types, return as is
        return obj;
    }

    /// <summary>
    /// Converts an object to a dictionary with string keys.
    /// Enhanced to handle null variables properly.
    /// </summary>
    private static IDictionary<string, object?> ToDictionary(object? obj)
    {
        if (obj == null || obj is DollarSign.NoParametersContext)
        {
            // Return an empty dictionary that allows any key access but returns null
            return new NullVariablesDictionary();
        }

        // Already a dictionary with string keys and nullable values
        if (obj is IDictionary<string, object?> dictNullable)
        {
            var newDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in dictNullable)
            {
                newDict[kvp.Key] = ConvertToEvalFriendlyObject(kvp.Value);
            }
            return newDict;
        }

        // Dictionary with string keys and non-nullable values
        if (obj is IDictionary<string, object> dictNonNullable)
        {
            var newDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in dictNonNullable)
            {
                newDict[kvp.Key] = ConvertToEvalFriendlyObject(kvp.Value);
            }
            return newDict;
        }

        // ExpandoObject (dynamic)
        if (obj is ExpandoObject expandoObj)
        {
            var dynamicDict = (IDictionary<string, object?>)expandoObj;
            var newDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in dynamicDict)
            {
                newDict[kvp.Key] = ConvertToEvalFriendlyObject(kvp.Value);
            }
            return newDict;
        }

        // Generic dictionary with string-compatible keys
        if (obj is IDictionary dictionary && obj.GetType().IsGenericType)
        {
            Type genericTypeDef = obj.GetType().GetGenericTypeDefinition();
            if (genericTypeDef == typeof(Dictionary<,>) || genericTypeDef == typeof(IDictionary<,>))
            {
                var keyType = obj.GetType().GetGenericArguments()[0];

                // Only process if keys can be converted to strings
                if (keyType == typeof(string) || keyType.IsPrimitive || keyType == typeof(Guid))
                {
                    var newDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        string key = entry.Key?.ToString() ?? string.Empty;
                        newDict[key] = ConvertToEvalFriendlyObject(entry.Value);
                    }
                    return newDict;
                }
            }
        }

        // Non-generic dictionary
        if (obj is IDictionary nonGenericDict)
        {
            var newDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in nonGenericDict)
            {
                string key = entry.Key?.ToString() ?? string.Empty;
                newDict[key] = ConvertToEvalFriendlyObject(entry.Value);
            }
            return newDict;
        }

        // For regular objects, extract properties
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        // Handle anonymous types specially
        if (IsAnonymousType(obj.GetType()))
        {
            // Convert to dictionary and return
            var converted = ConvertToEvalFriendlyObject(obj);
            if (converted is IDictionary<string, object?> convertedDict)
            {
                return convertedDict;
            }
        }

        // Regular object property extraction
        if (obj != null && !(obj is DollarSign.NoParametersContext))
        {
            foreach (var property in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.CanRead)
                {
                    var propValue = property.GetValue(obj);
                    result[property.Name] = ConvertToEvalFriendlyObject(propValue);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Merges two dictionaries with case-insensitive keys.
    /// Local variables take precedence over global ones.
    /// </summary>
    private static IDictionary<string, object?> MergeDictionaries(
        IDictionary<string, object?> globalDict,
        IDictionary<string, object?> localDict)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        // Add all global variables first
        foreach (var kvp in globalDict)
        {
            result[kvp.Key] = kvp.Value;
        }

        // Override with local variables (these take precedence)
        foreach (var kvp in localDict)
        {
            result[kvp.Key] = kvp.Value;
        }

        return result;
    }

    /// <summary>
    /// Resolves a property value from an object using reflection with case-insensitive matching.
    /// </summary>
    public static object? ResolvePropertyValueFromObject(object source, string propertyName)
    {
        if (source == null || string.IsNullOrEmpty(propertyName)) return null;

        // Handle dictionary types first for better performance with dictionaries
        if (source is IDictionary<string, object?> dictNullable)
        {
            // Try exact match first
            if (dictNullable.TryGetValue(propertyName, out var value))
            {
                return value;
            }

            // Try case-insensitive lookup
            foreach (var key in dictNullable.Keys)
            {
                if (string.Equals(key, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    return dictNullable[key];
                }
            }

            // Not found
            return null;
        }

        if (source is IDictionary<string, object> dictNonNullable)
        {
            // Try exact match first
            if (dictNonNullable.TryGetValue(propertyName, out var value))
            {
                return value;
            }

            // Try case-insensitive lookup
            foreach (var key in dictNonNullable.Keys)
            {
                if (string.Equals(key, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    return dictNonNullable[key];
                }
            }

            // Not found
            return null;
        }

        // Handle general IDictionary interface
        if (source is IDictionary dict)
        {
            // Try exact match
            if (dict.Contains(propertyName))
            {
                return dict[propertyName];
            }

            // Try case-insensitive lookup for string keys
            foreach (var key in dict.Keys)
            {
                if (key is string strKey && string.Equals(strKey, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    return dict[key];
                }
            }

            // Not found
            return null;
        }

        // Fall back to reflection for regular objects
        try
        {
            var property = source.GetType().GetProperty(propertyName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (property != null && property.CanRead)
            {
                return property.GetValue(source);
            }
        }
        catch
        {
            // Silently handle reflection exceptions
        }

        return null;
    }

    /// <summary>
    /// Special dictionary that returns null for any key access when variables parameter is null.
    /// This mimics C# string interpolation behavior with null context.
    /// </summary>
    private class NullVariablesDictionary : IDictionary<string, object?>
    {
        public object? this[string key]
        {
            get => null; // Always return null for any key
            set { } // Ignore sets
        }

        public ICollection<string> Keys => Array.Empty<string>();
        public ICollection<object?> Values => Array.Empty<object?>();
        public int Count => 0;
        public bool IsReadOnly => true;

        public void Add(string key, object? value) { }
        public void Add(KeyValuePair<string, object?> item) { }
        public void Clear() { }
        public bool Contains(KeyValuePair<string, object?> item) => false;
        public bool ContainsKey(string key) => true; // Pretend all keys exist but return null
        public void CopyTo(KeyValuePair<string, object?>[] array, int arrayIndex) { }
        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => Enumerable.Empty<KeyValuePair<string, object?>>().GetEnumerator();
        public bool Remove(string key) => false;
        public bool Remove(KeyValuePair<string, object?> item) => false;
        public bool TryGetValue(string key, out object? value)
        {
            value = null;
            return true; // Always succeed but return null
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}