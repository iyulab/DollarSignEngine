namespace DollarSignEngine.Internals;

/// <summary>
/// Helper methods for processing context data for evaluation.
/// </summary>
internal static class DataPreparationHelper
{
    /// <summary>
    /// Determines if type is anonymous type.
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
    /// Converts object to format more suitable for evaluation.
    /// </summary>
    public static object? ConvertToEvalFriendlyObject(object? obj)
    {
        if (obj == null) return null;
        Type type = obj.GetType();

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

        if (type.IsArray)
        {
            var array = (Array)obj;
            Type? elementType = type.GetElementType();

            if (elementType == null) return obj;

            if (elementType == typeof(int) ||
                elementType == typeof(long) ||
                elementType == typeof(float) ||
                elementType == typeof(double) ||
                elementType == typeof(decimal))
            {
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

            if (elementType.IsPrimitive || elementType == typeof(string) || elementType == typeof(decimal) ||
                elementType == typeof(DateTime) || elementType == typeof(DateTimeOffset) ||
                (!IsAnonymousType(elementType) && !typeof(IEnumerable).IsAssignableFrom(elementType) || elementType == typeof(string)))
            {
                return obj;
            }

            var items = new List<object?>(array.Length);
            foreach (var item in array)
            {
                items.Add(ConvertToEvalFriendlyObject(item));
            }
            return items.ToArray();
        }

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

        if (obj is IDictionary dictionary && type.IsGenericType)
        {
            var genericTypeDef = type.GetGenericTypeDefinition();
            if (genericTypeDef == typeof(Dictionary<,>) || genericTypeDef == typeof(IDictionary<,>))
            {
                var keyType = type.GetGenericArguments()[0];

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

        if (obj is IList list && type.IsGenericType)
        {
            Type listType = type.GetGenericTypeDefinition();
            if (listType == typeof(List<>) || listType == typeof(IList<>))
            {
                Type elementType = type.GetGenericArguments()[0];

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

        if (obj is IEnumerable enumerable && !(obj is string))
        {
            var items = new List<object?>();
            foreach (var item in enumerable)
            {
                items.Add(ConvertToEvalFriendlyObject(item));
            }
            return items;
        }

        return obj;
    }

    /// <summary>
    /// Resolves property value from object using reflection with case-insensitive matching.
    /// </summary>
    public static object? ResolvePropertyValueFromObject(object source, string propertyName)
    {
        if (source == null || string.IsNullOrEmpty(propertyName)) return null;

        if (source is IDictionary<string, object?> dictNullable)
        {
            if (dictNullable.TryGetValue(propertyName, out var value))
            {
                return value;
            }

            foreach (var key in dictNullable.Keys)
            {
                if (string.Equals(key, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    return dictNullable[key];
                }
            }

            return null;
        }

        if (source is IDictionary<string, object> dictNonNullable)
        {
            if (dictNonNullable.TryGetValue(propertyName, out var value))
            {
                return value;
            }

            foreach (var key in dictNonNullable.Keys)
            {
                if (string.Equals(key, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    return dictNonNullable[key];
                }
            }

            return null;
        }

        if (source is IDictionary dict)
        {
            if (dict.Contains(propertyName))
            {
                return dict[propertyName];
            }

            foreach (var key in dict.Keys)
            {
                if (key is string strKey && string.Equals(strKey, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    return dict[key];
                }
            }

            return null;
        }

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
}