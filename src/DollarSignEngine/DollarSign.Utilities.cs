using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;

namespace DollarSignEngine;

/// <summary>
/// Contains utility methods for the DollarSign class.
/// </summary>
public static partial class DollarSign
{
    /// <summary>
    /// Checks if an expression represents an array/list indexer access or nested access (e.g., "array[0]", "dict["key"]", "dict["key"].Count")
    /// </summary>
    private static bool IsArrayIndexAccess(string expression, out string arrayName, out string indexOrKey)
    {
        arrayName = string.Empty;
        indexOrKey = string.Empty;

        // Match patterns like:
        // array[0]
        // dict["key"] or dict['key']
        // items[^1]  (from end indexing)
        // dict["key"].Property
        var match = Regex.Match(
            expression,
            @"^([a-zA-Z_][a-zA-Z0-9_]*)\[(?:""([^""]*)""|'([^']*)'|(\^?\d+))\](?:\.([a-zA-Z_][a-zA-Z0-9_]*))?$"
        );

        if (match.Success)
        {
            arrayName = match.Groups[1].Value;

            // Index could be in one of three capture groups: quoted string, quoted with single quotes, or number
            if (match.Groups[2].Success) // "key"
                indexOrKey = match.Groups[2].Value;
            else if (match.Groups[3].Success) // 'key'
                indexOrKey = match.Groups[3].Value;
            else if (match.Groups[4].Success) // ^1 or 0
                indexOrKey = match.Groups[4].Value;

            // If there's a property access (e.g., .Count), append it to the indexOrKey
            if (match.Groups[5].Success)
            {
                indexOrKey += $".{match.Groups[5].Value}";
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if a string is a simple identifier (variable name).
    /// Uses Roslyn's SyntaxFacts to validate the identifier
    /// </summary>
    private static bool IsSimpleIdentifier(string expression)
    {
        // Leverage Roslyn for more accurate identifier validation
        return SyntaxFacts.IsValidIdentifier(expression);
    }

    /// <summary>
    /// Checks if a string is a safe C# identifier for variable declarations
    /// Uses Roslyn's SyntaxFacts to validate the identifier and checks if it's a keyword
    /// </summary>
    private static bool IsSafeIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        // Use Roslyn's syntax facts to check for valid identifier
        if (!SyntaxFacts.IsValidIdentifier(name))
            return false;

        // Check if it's a keyword using SyntaxFacts
        var kind = SyntaxFacts.GetKeywordKind(name);
        return !SyntaxFacts.IsKeywordKind(kind) && !SyntaxFacts.IsContextualKeyword(kind);
    }

    /// <summary>
    /// Checks if a string is a property chain (object.property).
    /// </summary>
    private static bool IsPropertyChain(string expression, out string objName, out string propName)
    {
        objName = string.Empty;
        propName = string.Empty;

        // Improved regex for property chain detection
        var match = Regex.Match(expression, @"^([a-zA-Z_][a-zA-Z0-9_]*)\.([a-zA-Z_][a-zA-Z0-9_]*)$");
        if (match.Success)
        {
            objName = match.Groups[1].Value;
            propName = match.Groups[2].Value;

            // Validate both parts are valid identifiers using Roslyn
            if (SyntaxFacts.IsValidIdentifier(objName) && SyntaxFacts.IsValidIdentifier(propName))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to get a property or field value from an object using reflection.
    /// Optimized for common types and performance.
    /// </summary>
    private static bool TryGetPropertyValue(object obj, string propertyName, out object? value)
    {
        value = null;

        try
        {
            // Fast path for common object types
            if (obj is IList list && propertyName == "Count")
            {
                value = list.Count;
                return true;
            }
            else if (obj is ICollection collection && propertyName == "Count")
            {
                value = collection.Count;
                return true;
            }
            else if (obj is Array array && propertyName == "Length")
            {
                value = array.Length;
                return true;
            }
            else if (obj is string str && propertyName == "Length")
            {
                value = str.Length;
                return true;
            }

            // Handle dictionary types more efficiently
            if (TryGetDictionaryValue(obj, propertyName, out var dictValue))
            {
                value = dictValue;
                return true;
            }

            // Use Type.GetProperty which is more efficient than reflection-based lookup
            Type type = obj.GetType();
            PropertyInfo? property = type.GetProperty(propertyName);
            if (property != null)
            {
                value = property.GetValue(obj);
                return true;
            }

            // Try field if property not found
            FieldInfo? field = type.GetField(propertyName);
            if (field != null)
            {
                value = field.GetValue(obj);
                return true;
            }

            // Last resort: check if the object is dynamic with custom property access
            if (obj is System.Dynamic.IDynamicMetaObjectProvider dynamic)
            {
                try
                {
                    // Use dynamic invocation to try accessing the property
                    dynamic dynamicObj = obj;
                    value = GetDynamicProperty(dynamicObj, propertyName);
                    return value != null;
                }
                catch
                {
                    // Failed to access dynamic property
                }
            }
        }
        catch
        {
            // Ignore exceptions and return false
        }

        return false;
    }

    /// <summary>
    /// Helper method to safely get a property from a dynamic object
    /// </summary>
    private static object? GetDynamicProperty(dynamic obj, string propertyName)
    {
        try
        {
            // Use reflection to get property since direct dynamic access 
            // would require a string literal property name
            var type = obj.GetType();
            var getMethod = type.GetMethod("get_Item", new[] { typeof(string) });

            if (getMethod != null)
            {
                return getMethod.Invoke(obj, new object[] { propertyName });
            }

            // Try direct property access via reflection
            var prop = type.GetProperty(propertyName);
            if (prop != null)
            {
                return prop.GetValue(obj);
            }
        }
        catch
        {
            // Ignore exceptions
        }

        return null;
    }

    /// <summary>
    /// Specialized method for dictionary access
    /// </summary>
    private static bool TryGetDictionaryValue(object obj, string key, out object? value)
    {
        value = null;

        // Try standard dictionary
        if (obj is IDictionary dict && dict.Contains(key))
        {
            value = dict[key];
            return true;
        }

        // Try generic dictionary with string key
        if (obj is IDictionary<string, object?> dictObj && dictObj.TryGetValue(key, out var objValue))
        {
            value = objValue;
            return true;
        }

        // Try dictionary with string key and string value
        if (obj is IDictionary<string, string> strDict && strDict.TryGetValue(key, out var strValue))
        {
            value = strValue;
            return true;
        }

        // Try to find any IDictionary<string, T> implementation using reflection
        Type type = obj.GetType();
        if (type.IsGenericType)
        {
            Type genericType = type.GetGenericTypeDefinition();
            if (genericType == typeof(Dictionary<,>) || genericType == typeof(IDictionary<,>))
            {
                Type[] typeArgs = type.GetGenericArguments();
                if (typeArgs.Length == 2 && typeArgs[0] == typeof(string))
                {
                    // Find and invoke TryGetValue method
                    MethodInfo? tryGetValue = type.GetMethod("TryGetValue");
                    if (tryGetValue != null)
                    {
                        var parameters = new object[] { key, null! };
                        bool success = (bool)tryGetValue.Invoke(obj, parameters)!;
                        if (success)
                        {
                            value = parameters[1];
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Helper method to get a type's default LINQ capabilities
    /// </summary>
    private static Type[] GetLinqSupportedInterfaces(Type type)
    {
        return type.GetInterfaces()
            .Where(i => i.IsGenericType &&
                  (i.GetGenericTypeDefinition() == typeof(IEnumerable<>) ||
                   i.GetGenericTypeDefinition() == typeof(ICollection<>) ||
                   i.GetGenericTypeDefinition() == typeof(IList<>)))
            .ToArray();
    }

    /// <summary>
    /// Checks if a type supports standard LINQ operations
    /// </summary>
    private static bool SupportsLinq(Type type)
    {
        if (type.IsArray)
            return true;

        if (type == typeof(string))
            return true;

        return GetLinqSupportedInterfaces(type).Length > 0;
    }

    /// <summary>
    /// Gets appropriate Enumerable.Cast<T> method for a specific type
    /// </summary>
    private static MethodInfo? GetEnumerableCastMethod(Type elementType)
    {
        try
        {
            return typeof(Enumerable)
                .GetMethods()
                .FirstOrDefault(m => m.Name == "Cast" && m.GetParameters().Length == 1)
                ?.MakeGenericMethod(elementType);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets an appropriate LINQ extension method for a collection and operation
    /// </summary>
    private static MethodInfo? GetLinqMethod(string methodName, Type collectionType, Type elementType)
    {
        try
        {
            var methods = typeof(Enumerable).GetMethods()
                .Where(m => m.Name == methodName)
                .ToArray();

            foreach (var method in methods)
            {
                if (method.IsGenericMethod)
                {
                    try
                    {
                        return method.MakeGenericMethod(elementType);
                    }
                    catch
                    {
                        // Continue to next method
                    }
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return null;
    }
}