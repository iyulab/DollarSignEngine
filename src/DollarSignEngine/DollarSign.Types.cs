using System.Text;

namespace DollarSignEngine;

/// <summary>
/// Type conversion and handling functionality for DollarSign.
/// </summary>
public static partial class DollarSign
{
    private static readonly Dictionary<string, string> typeAliases = new()
    {
        { typeof(int).FullName!, "int" },
        { typeof(long).FullName!, "long" },
        { typeof(float).FullName!, "float" },
        { typeof(double).FullName!, "double" },
        { typeof(decimal).FullName!, "decimal" },
        { typeof(bool).FullName!, "bool" },
        { typeof(string).FullName!, "string" },
        { typeof(object).FullName!, "object" }
    };

    private static readonly Dictionary<Type, Func<object, string>> typeConverters = new()
    {
        { typeof(string), value => $"\"{value.ToString()!.Replace("\"", "\\\"")}\"" },
        { typeof(DateTime), value => $"DateTime.Parse(\"{value}\")" },
        { typeof(bool), value => value.ToString()!.ToLower() }
    };

    /// <summary>
    /// Gets a type alias for a given type, if available.
    /// </summary>
    internal static string GetTypeAlias(Type type)
    {
        return typeAliases.TryGetValue(type.FullName!, out var alias) ? alias : type.Name;
    }

    /// <summary>
    /// Converts array elements to a string representation.
    /// </summary>
    internal static void ConvertArrayElements(Array array, StringBuilder code, int[] lengths, int dimension, int[] indices, IDictionary<string, string> scriptParams)
    {
        if (dimension == lengths.Length)
        {
            var value = array.GetValue(indices);
            code.Append(ConvertValue(value, scriptParams));
            return;
        }

        code.Append("{ ");
        for (int i = 0; i < lengths[dimension]; i++)
        {
            var newIndices = new List<int>(indices) { i }.ToArray();
            ConvertArrayElements(array, code, lengths, dimension + 1, newIndices, scriptParams);
            if (i < lengths[dimension] - 1) code.Append(", ");
        }
        code.Append(" }");
    }
}