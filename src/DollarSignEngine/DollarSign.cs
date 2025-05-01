using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System.Collections;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace DollarSignEngine;

/// <summary>
/// Provides functionality to dynamically evaluate C# expressions using interpolation strings and parameters.
/// </summary>
public static class DollarSign
{
    /// <summary>
    /// Asynchronously evaluates a given C# expression as a string and returns the result.
    /// </summary>
    public static async Task<string> EvalAsync(string expression, object? parameter = null, DollarSignOption? option = null)
    {
        try
        {
            var options = BuildScriptOptions(option);
            var script = BuildCsScriptWithRoslyn(expression, parameter);

            // Enable debug logging if requested
            if (option?.EnableDebugLogging == true)
            {
                Console.WriteLine($"Generated script:\n{script}");
            }

            var result = await CSharpScript.EvaluateAsync<string>(script, options);
            return result;
        }
        catch (CompilationErrorException compilationError)
        {
            throw new DollarSignEngineException($"CompilationError: {compilationError.Message}", compilationError);
        }
        catch (Exception e)
        {
            throw new DollarSignEngineException($"Error: {e.Message}", e);
        }
    }

    /// <summary>
    /// Builds script options with appropriate references and imports.
    /// </summary>
    private static ScriptOptions BuildScriptOptions(DollarSignOption? option)
    {
        var options = ScriptOptions.Default
            .WithImports("System", "System.Collections.Generic", "System.Linq", "System.Dynamic")
            .AddReferences(
                Assembly.Load("mscorlib"),
                Assembly.Load("System"),
                Assembly.Load("System.Core"),
                Assembly.Load("Microsoft.CSharp")
            );

        if (option != null)
        {
            if (option.AdditionalNamespaces.Count > 0)
            {
                options = options.WithImports(option.AdditionalNamespaces);
            }

            if (option.AdditionalAssemblies.Count > 0)
            {
                var assemblies = option.AdditionalAssemblies
                    .Select(Assembly.Load)
                    .ToArray();
                options = options.AddReferences(assemblies);
            }
        }

        return options;
    }

    /// <summary>
    /// Builds a C# script from the expression and parameters using Roslyn.
    /// </summary>
    private static string BuildCsScriptWithRoslyn(string expression, object? parameter)
    {
        var script = new StringBuilder();

        // Add utility methods for safe property access
        AppendHelperMethods(script);

        // Add parameter declarations
        if (parameter != null)
        {
            AppendParameterDeclarations(script, parameter);
        }

        // Process the expression using Roslyn-based approach
        string interpolatedExpression = InterpolationParser.ProcessInterpolation(expression);

        // Add the interpolated expression and return statement
        script.AppendLine($"return {interpolatedExpression};");

        return script.ToString();
    }

    /// <summary>
    /// Appends parameter declarations to the script.
    /// </summary>
    private static void AppendParameterDeclarations(StringBuilder script, object parameter)
    {
        // Direct parameter - expose all properties directly
        if (parameter is not IDictionary<string, object?> dict)
        {
            // Store the original parameter as "_" for the helper methods to access
            script.AppendLine($"var _ = {SerializeObjectAsExpression(parameter)};");

            // Expose all public properties as top-level variables
            foreach (var prop in parameter.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                script.AppendLine($"var {prop.Name} = _.{prop.Name};");
            }
        }
        else
        {
            // Dictionary parameter - declare each key-value pair
            foreach (var pair in dict)
            {
                if (pair.Value != null)
                {
                    script.AppendLine($"var {pair.Key} = {SerializeObjectAsExpression(pair.Value)};");
                }
            }
        }
    }

    /// <summary>
    /// Serializes an object as a C# expression that can be evaluated.
    /// </summary>
    private static string SerializeObjectAsExpression(object? obj)
    {
        if (obj == null) return "null";

        var type = obj.GetType();

        // Handle primitive types
        if (type == typeof(string))
            return $"\"{obj.ToString()!.Replace("\"", "\\\"")}\"";
        if (type == typeof(bool))
            return obj.ToString()!.ToLower();
        if (type == typeof(char))
            return $"'{obj}'";
        if (type == typeof(DateTime))
            return $"DateTime.Parse(\"{obj}\")";

        // Handle numeric types - let C# handle them directly
        if (obj is byte || obj is sbyte || obj is short || obj is ushort ||
            obj is int || obj is uint || obj is long || obj is ulong ||
            obj is float || obj is double || obj is decimal)
            return obj.ToString()!;

        // Handle arrays
        if (type.IsArray)
        {
            var array = (Array)obj;
            var elements = new StringBuilder();
            elements.Append("new [] { ");

            for (int i = 0; i < array.Length; i++)
            {
                elements.Append(SerializeObjectAsExpression(array.GetValue(i)));
                if (i < array.Length - 1) elements.Append(", ");
            }

            elements.Append(" }");
            return elements.ToString();
        }

        // Handle dictionaries specifically - improved to handle key type properly
        if (obj is IDictionary dictionary)
        {
            var dictType = type.GetGenericArguments();
            string keyType = "object";
            string valueType = "object";

            // Try to determine generic type arguments
            if (dictType.Length == 2)
            {
                keyType = dictType[0].Name;
                valueType = dictType[1].Name;
            }

            var elements = new StringBuilder();
            elements.Append($"new System.Collections.Generic.Dictionary<{keyType}, {valueType}> {{ ");

            bool first = true;
            foreach (DictionaryEntry entry in dictionary)
            {
                if (!first) elements.Append(", ");

                // Properly handle key serialization based on type
                string keyString;
                if (entry.Key is string)
                    keyString = $"\"{entry.Key.ToString()!.Replace("\"", "\\\"")}\"";
                else
                    keyString = SerializeObjectAsExpression(entry.Key);

                elements.Append("{ ")
                       .Append(keyString)
                       .Append(", ")
                       .Append(SerializeObjectAsExpression(entry.Value))
                       .Append(" }");

                first = false;
            }

            elements.Append(" }");
            return elements.ToString();
        }

        // Handle collections
        if (obj is IEnumerable enumerable && obj is not string)
        {
            var elements = new StringBuilder();
            elements.Append("new System.Collections.Generic.List<object> { ");

            bool first = true;
            foreach (var item in enumerable)
            {
                if (!first) elements.Append(", ");
                elements.Append(SerializeObjectAsExpression(item));
                first = false;
            }

            elements.Append(" }");
            return elements.ToString();
        }

        // For complex objects, create an anonymous type with equivalent properties
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        if (properties.Length > 0)
        {
            var objInit = new StringBuilder();
            objInit.Append("new { ");

            bool first = true;
            foreach (var prop in properties)
            {
                if (!first) objInit.Append(", ");
                objInit.Append($"{prop.Name} = {SerializeObjectAsExpression(prop.GetValue(obj))}");
                first = false;
            }

            objInit.Append(" }");
            return objInit.ToString();
        }

        // Fallback to ToString() for anything else
        return $"\"{obj.ToString()!.Replace("\"", "\\\"")}\"";
    }

    /// <summary>
    /// Appends helper methods to the script.
    /// </summary>
    private static void AppendHelperMethods(StringBuilder script)
    {
        script.AppendLine(@"
// Helper function for safe property access
object? SafePropertyAccess(object? obj, string propertyPath)
{
    if (obj == null) return null;
    
    var parts = propertyPath.Split('.');
    var current = obj;
    
    foreach (var part in parts)
    {
        if (current == null) return null;
        
        // Handle indexer syntax like Items[0] or Dict[""key""]
        if (part.Contains('[') && part.Contains(']'))
        {
            string propName = part.Substring(0, part.IndexOf('['));
            string indexStr = part.Substring(part.IndexOf('[') + 1, part.IndexOf(']') - part.IndexOf('[') - 1);
            
            // Get the collection property
            var propInfo = current.GetType().GetProperty(propName);
            if (propInfo == null) return null;
            
            var collection = propInfo.GetValue(current);
            if (collection == null) return null;
            
            // Access by indexer
            try {
                if (collection is System.Collections.IDictionary dict)
                {
                    // Remove quotes if present
                    if (indexStr.StartsWith('""') && indexStr.EndsWith('""'))
                    {
                        indexStr = indexStr.Substring(1, indexStr.Length - 2);
                    }
                    
                    current = dict[indexStr];
                }
                else if (collection is System.Collections.IList list)
                {
                    int index = int.Parse(indexStr);
                    current = list[index];
                }
                else
                {
                    // Try to invoke an indexer through reflection
                    var indexerProp = collection.GetType().GetProperties()
                        .FirstOrDefault(p => p.GetIndexParameters().Length > 0);
                    
                    if (indexerProp != null)
                    {
                        // Convert the index to the appropriate type
                        var indexParams = indexerProp.GetIndexParameters();
                        var indexType = indexParams[0].ParameterType;
                        var convertedIndex = Convert.ChangeType(indexStr, indexType);
                        
                        current = indexerProp.GetValue(collection, new[] { convertedIndex });
                    }
                    else
                    {
                        return null; // No indexer found
                    }
                }
            }
            catch {
                return null; // Index access failed
            }
        }
        else
        {
            // Regular property access
            var propInfo = current.GetType().GetProperty(part);
            if (propInfo == null) return null;
            
            current = propInfo.GetValue(current);
        }
    }
    
    return current;
}

// Helper function to format values
string FormatValue(object? value, string? format = null, int? alignment = null)
{
    if (value == null) return ""null"";
    
    string result;
    
    // Apply format if provided
    if (!string.IsNullOrEmpty(format))
    {
        if (value is IFormattable formattable)
        {
            result = formattable.ToString(format, System.Globalization.CultureInfo.CurrentCulture);
        }
        else
        {
            result = value.ToString();
        }
    }
    else
    {
        result = value.ToString();
    }
    
    // Apply alignment if provided
    if (alignment.HasValue)
    {
        int spaces = Math.Abs(alignment.Value);
        if (alignment.Value > 0)
        {
            result = result.PadLeft(spaces);
        }
        else if (alignment.Value < 0)
        {
            result = result.PadRight(spaces);
        }
    }
    
    return result;
}
");
    }
}