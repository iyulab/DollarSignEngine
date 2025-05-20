using System.Collections.Generic;
using System.Dynamic;
using System.Reflection;

namespace DollarSignEngine;

public static class DollarSign
{
    private static readonly ExpressionEvaluator Evaluator = new();

    public class NoParametersContext { }

    internal static bool IsAnonymousType(Type? type)
    {
        if (type == null) return false;
        return Attribute.IsDefined(type, typeof(CompilerGeneratedAttribute), false)
            && type.IsGenericType && type.Name.Contains("AnonymousType")
            && (type.Name.StartsWith("<>") || type.Name.StartsWith("VB$"))
            && (type.Attributes & TypeAttributes.NotPublic) == TypeAttributes.NotPublic;
    }

    internal static object? ConvertToEvalFriendlyObject(object? obj)
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

            // For numeric arrays, convert to IEnumerable<T> or List<T> to ensure LINQ extension methods work
            if (elementType == typeof(int) ||
                elementType == typeof(long) ||
                elementType == typeof(float) ||
                elementType == typeof(double) ||
                elementType == typeof(decimal))
            {
                // Convert array to List<T> which has better LINQ support
                var listType = typeof(List<>).MakeGenericType(elementType);
                var list = Activator.CreateInstance(listType);
                var addMethod = listType.GetMethod("Add");

                if (addMethod != null)
                {
                    foreach (var item in array)
                    {
                        addMethod.Invoke(list, new[] { item });
                    }
                    return list;
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

        if (obj is IDictionary<string, object?> objDict)
        {
            // Use StringComparer.OrdinalIgnoreCase explicitly 
            var newDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in objDict)
            {
                newDict[kvp.Key] = ConvertToEvalFriendlyObject(kvp.Value);
            }
            return newDict;
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

    private static IDictionary<string, Type> GetPropertyTypes(object? obj)
    {
        var types = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        if (obj == null || obj is NoParametersContext) return types;

        if (obj is ExpandoObject expando)
        {
            foreach (var kvp in (IDictionary<string, object?>)expando)
            {
                types[kvp.Key] = kvp.Value?.GetType() ?? typeof(object);
            }
        }
        else if (obj is IDictionary<string, object?> dictObj)
        {
            foreach (var kvp in dictObj)
            {
                types[kvp.Key] = kvp.Value?.GetType() ?? typeof(object);
            }
        }
        else
        {
            foreach (var property in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.CanRead)
                {
                    types[property.Name] = property.PropertyType;
                }
            }
        }
        return types;
    }

    private static IDictionary<string, object?> ToDictionary(object? obj)
    {
        if (obj == null || obj is NoParametersContext) return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (obj is IDictionary<string, object?> alreadyDict)
        {
            // Use StringComparer.OrdinalIgnoreCase explicitly
            var newProcessedDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in alreadyDict)
            {
                newProcessedDict[kvp.Key] = ConvertToEvalFriendlyObject(kvp.Value);
            }
            return newProcessedDict;
        }

        if (obj is ExpandoObject)
        {
            return (IDictionary<string, object?>)ConvertToEvalFriendlyObject(obj)!;
        }

        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        object objectToReflect = IsAnonymousType(obj.GetType()) ? ConvertToEvalFriendlyObject(obj)! : obj;

        if (objectToReflect is IDictionary<string, object?> reflectedAsDict)
        {
            return reflectedAsDict;
        }

        if (objectToReflect != null && !(objectToReflect is NoParametersContext))
        {
            foreach (var property in objectToReflect.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.CanRead)
                {
                    var propValue = property.GetValue(objectToReflect);
                    dict[property.Name] = ConvertToEvalFriendlyObject(propValue);
                }
            }
        }
        return dict;
    }

    // Merge two dictionaries with case-insensitive keys
    // Local variables take precedence over global ones
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

    public static async Task<string> EvalAsync(
        string expression,
        object? variables = null,
        DollarSignOptions? options = null)
    {
        if (string.IsNullOrEmpty(expression))
            return string.Empty;

        var effectiveOptions = options?.Clone() ?? DollarSignOptions.Default;
        Logger.Debug($"[DollarSign.EvalAsync] Expression: {expression}");

        object? contextForEvaluator;
        IDictionary<string, Type>? globalVariableTypes = null;

        // Handle the effective variables based on local and global data
        var localVariables = variables ?? new NoParametersContext();
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
        contextForEvaluator = new ScriptHost(mergedDict);

        try
        {
            string result = await Evaluator.EvaluateAsync(expression, contextForEvaluator, effectiveOptions);
            return result;
        }
        catch (DollarSignEngineException)
        {
            if (effectiveOptions.ThrowOnError) throw;
            return string.Empty;
        }
        catch (Exception ex)
        {
            Logger.Debug($"[DollarSign.EvalAsync] General Exception: {ex.GetType().Name}: {ex.Message}");
            if (effectiveOptions.ThrowOnError)
                throw new DollarSignEngineException($"Error evaluating expression: \"{expression}\"", ex);
            return string.Empty;
        }
    }

    public static string Eval(
        string expression,
        object? variables = null,
        DollarSignOptions? options = null)
        => EvalAsync(expression, variables, options).GetAwaiter().GetResult();

    public static void ClearCache() => Evaluator.ClearCache();

    // Changed from private to internal to support extensions
    internal static object? ResolvePropertyValueFromObject(object source, string propertyName)
    {
        if (source == null || string.IsNullOrEmpty(propertyName)) return null;
        try
        {
            var property = source.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            return property?.GetValue(source);
        }
        catch { return null; }
    }
}

public class ScriptHost
{
    public IDictionary<string, object?> Globals { get; }

    public ScriptHost(IDictionary<string, object?> globals)
    {
        Globals = globals ?? new Dictionary<string, object?>();
    }
}