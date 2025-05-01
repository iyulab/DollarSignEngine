using System.Collections;
using System.Dynamic;
using System.Reflection;
using System.Text;

namespace DollarSignEngine;

/// <summary>
/// Helper methods for DollarSign, including parameter conversion and type handling.
/// </summary>
public static partial class DollarSign
{
    private static int expandoCounter = 0;

    /// <summary>
    /// Converts a parameter object to a dictionary of property names and values.
    /// This method handles different types of objects including dictionaries, anonymous types, and named types.
    /// </summary>
    internal static IDictionary<string, object?> ConvertParameterToDictionary(object parameter)
    {
        // If parameter is already a dictionary, cast it
        if (parameter is IDictionary<string, object?> dict)
        {
            return dict;
        }
        // For other IDictionary types, convert to Dictionary<string, object?>
        else if (parameter is IDictionary idictionary)
        {
            var resultDict = new Dictionary<string, object?>();
            foreach (DictionaryEntry entry in idictionary)
            {
                resultDict[entry.Key.ToString() ?? string.Empty] = entry.Value;
            }
            return resultDict;
        }
        // For ExpandoObject
        else if (parameter is ExpandoObject expando)
        {
            return (IDictionary<string, object?>)expando;
        }
        // For any other object (named type, anonymous type), use reflection
        else
        {
            var resultDict = new Dictionary<string, object?>();

            // First, wrap the object in a container to allow safe null checking
            resultDict["_"] = parameter;

            // Then add all properties directly for direct access
            var properties = parameter.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                resultDict[prop.Name] = prop.GetValue(parameter);
            }

            return resultDict;
        }
    }

    /// <summary>
    /// Converts a C# value to a string representation that can be used in a script.
    /// </summary>
    internal static string ConvertValue(object? value, IDictionary<string, string> scriptParams)
    {
        if (value == null) return "null";

        var type = value.GetType();
        if (typeConverters.TryGetValue(type, out var converter))
        {
            return converter(value);
        }
        else if (value is ExpandoObject expando)
        {
            var functionName = $"GetExpandoObject{expandoCounter++}";
            var keyValuePairsCode = new StringBuilder();
            foreach (var kvp in (IDictionary<string, object?>)expando)
            {
                var keyStr = kvp.Key;
                var valueStr = ConvertValue(kvp.Value, scriptParams);
                keyValuePairsCode.AppendLine($"expando.{keyStr} = {valueStr};");
            }

            scriptParams.Add(functionName, $@"
dynamic {functionName}()
{{
    dynamic expando = new System.Dynamic.ExpandoObject();
{keyValuePairsCode}
    return expando;
}}
");
            return $"{functionName}()";
        }
        else if (value is IDictionary dictionary && value.GetType().IsGenericType)
        {
            var dict = dictionary;
            var keyValuePairsCode = new StringBuilder();
            var keyType = value.GetType().GetGenericArguments()[0];
            var valueType = value.GetType().GetGenericArguments()[1];
            foreach (DictionaryEntry kvp in dict)
            {
                var keyStr = ConvertValue(kvp.Key, scriptParams);
                var valueStr = ConvertValue(kvp.Value, scriptParams);
                keyValuePairsCode.Append($"{{ {keyStr}, {valueStr} }}, ");
            }
            if (keyValuePairsCode.Length > 2) keyValuePairsCode.Length -= 2; // 마지막 쉼표 제거
            return $"new Dictionary<{keyType.Name}, {valueType.Name}>() {{ {keyValuePairsCode} }}";
        }
        else if (value is Array array && type.IsArray && type.GetElementType()!.Name.StartsWith("<>f__AnonymousType") != true)
        {
            var elementType = GetTypeAlias(type.GetElementType()!);
            var ranks = Enumerable.Range(0, type.GetArrayRank()).Select(array.GetLength).ToArray();
            var elements = new StringBuilder();
            ConvertArrayElements(array, elements, ranks, 0, [], scriptParams);

            return $"new {elementType}[{string.Join(',', ranks.Select(r => ""))}] {elements}";
        }
        else if (value is IEnumerable enumerable && value is not string)
        {
            var itemsCode = new StringBuilder();
            foreach (var item in enumerable)
            {
                var itemCode = ConvertValue(item, scriptParams);
                itemsCode.Append($"{itemCode}, ");
            }
            if (itemsCode.Length > 2) itemsCode.Length -= 2; // 마지막 쉼표 제거
            return $"new[] {{ {itemsCode} }}";
        }
        else if (type.FullName!.StartsWith("<>f__AnonymousType") || !type.IsPrimitive && !type.IsEnum && type != typeof(string))
        {
            // 익명 객체 및 사용자 정의 타입 처리 - null 값 처리 추가
            var props = type.GetProperties();
            var objInit = new StringBuilder();

            foreach (var prop in props)
            {
                var propValue = prop.GetValue(value);
                if (propValue == null)
                {
                    // null 값은 생략하지 않고 명시적으로 null로 변환
                    objInit.Append($"{prop.Name} = null, ");
                }
                else
                {
                    objInit.Append($"{prop.Name} = {ConvertValue(propValue, scriptParams)}, ");
                }
            }

            if (objInit.Length > 2) objInit.Length -= 2; // 마지막 쉼표 제거

            return $"new {{ {objInit} }}";
        }
        else if (type == typeof(string))
        {
            return $"\"{value.ToString()!.Replace("\"", "\\\"")}\"";
        }
        else
        {
            return value.ToString()!;
        }
    }
}