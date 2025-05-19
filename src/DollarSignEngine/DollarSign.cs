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
            // FIX: Use StringComparer.OrdinalIgnoreCase explicitly instead of objDict.Comparer
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
            // FIX: Use StringComparer.OrdinalIgnoreCase explicitly instead of alreadyDict.Comparer
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

        object effectiveVariables = variables ?? new NoParametersContext();

        if (!(effectiveVariables is NoParametersContext))
        {
            globalVariableTypes = GetPropertyTypes(effectiveVariables);
            effectiveOptions.GlobalVariableTypes = globalVariableTypes;

            if (globalVariableTypes.Any() || effectiveVariables is IDictionary<string, object> || effectiveVariables is IDictionary<string, object?> || effectiveVariables is ExpandoObject)
            {
                var globalsDict = ToDictionary(effectiveVariables);
                contextForEvaluator = new ScriptHost(globalsDict);
            }
            else
            {
                contextForEvaluator = ConvertToEvalFriendlyObject(effectiveVariables);
            }
        }
        else
        {
            contextForEvaluator = new NoParametersContext();
            effectiveOptions.GlobalVariableTypes = new Dictionary<string, Type>();
        }

        if (effectiveVariables != null &&
            !(effectiveVariables is IDictionary<string, object?>) &&
            !(effectiveVariables is ExpandoObject) &&
            effectiveOptions.VariableResolver == null &&
            !(contextForEvaluator is ScriptHost))
        {
            effectiveOptions.VariableResolver = name => ResolvePropertyValueFromObject(effectiveVariables, name);
        }

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

    private static object? ResolvePropertyValueFromObject(object source, string propertyName)
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