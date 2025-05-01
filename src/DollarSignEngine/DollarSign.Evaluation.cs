using System.Collections;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace DollarSignEngine;

/// <summary>
/// Contains expression evaluation functionality for the DollarSign class.
/// </summary>
public static partial class DollarSign
{
    /// <summary>
    /// Evaluates a C# expression asynchronously and returns the result.
    /// </summary>
    private static async Task<object?> EvaluateExpressionAsync(string expression, object? parameter, DollarSignOption option)
    {
        Log.Debug($"Evaluating expression: {expression}", option);
        Log.Debug($"With parameter type: {parameter?.GetType().Name ?? "null"}", option);

        // Try variable resolver callback if provided
        if (option.VariableResolver != null && option.PreferCallbackResolution)
        {
            try
            {
                var value = option.VariableResolver(expression, parameter);
                if (value != null)
                {
                    Log.Debug($"Callback resolver succeeded: {value}", option);
                    return value;
                }
            }
            catch (Exception ex)
            {
                Log.Debug($"Callback resolver failed: {ex.Message}", option);

                if (option.StrictParameterAccess)
                {
                    throw;
                }
            }
        }

        // For direct property access optimizations (when enabled)
        if (parameter != null && option.OptimizeCollectionAccess)
        {
            // Simple property access optimization
            if (IsSimpleIdentifier(expression))
            {
                if (TryGetPropertyValue(parameter, expression, out var value))
                {
                    Log.Debug($"Direct property access succeeded: {value}", option);
                    return value;
                }
            }

            // Property chain optimization (obj.prop)
            if (IsPropertyChain(expression, out var objName, out var propName))
            {
                if (TryGetPropertyValue(parameter, objName, out var objValue) && objValue != null)
                {
                    if (TryGetPropertyValue(objValue, propName, out var propValue))
                    {
                        Log.Debug($"Property chain access succeeded: {propValue}", option);
                        return propValue;
                    }
                }
            }

            // Array/Collection indexer optimization (arr[0], dict["key"])
            if (IsArrayIndexAccess(expression, out var arrayName, out var indexOrKey))
            {
                if (TryGetPropertyValue(parameter, arrayName, out var collection) && collection != null)
                {
                    if (HandleCollectionAccess(collection, indexOrKey, out var result))
                    {
                        Log.Debug($"Collection access succeeded: {result}", option);
                        return result;
                    }
                }
            }

            // Try to handle LINQ expressions directly where possible
            if (IsLinqExpression(expression))
            {
                // Try to get the target collection
                string collectionName;
                if (TryGetCollectionTarget(expression, out collectionName))
                {
                    if (TryGetPropertyValue(parameter, collectionName, out var collection) && collection != null)
                    {
                        if (TryHandleLinqOperationDirectly(expression, collection, out var linqResult))
                        {
                            Log.Debug($"Direct LINQ operation succeeded: {linqResult}", option);
                            return linqResult;
                        }
                    }
                }
            }
        }

        // Use Roslyn script evaluation for all expressions
        try
        {
            Log.Debug($"Using Roslyn evaluation for: {expression}", option);
            return await CompileAndEvaluateExpressionAsync(expression, parameter, option);
        }
        catch (Exception ex)
        {
            Log.Debug($"Expression evaluation error: {ex.Message}", option);

            if (option.ThrowOnMissingParameter)
                throw;

            return null;
        }
    }

    /// <summary>
    /// Compiles and evaluates a C# expression using Roslyn with enhanced type handling.
    /// </summary>
    private static async Task<object?> CompileAndEvaluateExpressionAsync(string expression, object? parameter, DollarSignOption option)
    {
        // Set up script options with necessary references and imports
        var scriptOptions = PrepareScriptOptions(option);

        // If no parameter is provided, just evaluate the expression directly
        if (parameter == null)
        {
            return await CSharpScript.EvaluateAsync<object>(expression, scriptOptions);
        }

        // Extract parameter properties to a dictionary
        var paramDict = ExtractPropertiesToDictionary(parameter);

        // Create the script globals
        var globals = new ScriptGlobals(paramDict);

        try
        {
            // Direct approach: create a script with parameter access
            string directScript = expression;

            // Try direct evaluation first with globals object accessible
            return await CSharpScript.EvaluateAsync<object>(directScript, scriptOptions, globals);
        }
        catch (Exception ex)
        {
            Log.Debug($"Direct evaluation failed: {ex.Message}", option);

            try
            {
                // Alternative approach: define variables explicitly
                var scriptBuilder = new StringBuilder();

                // Add required imports
                scriptBuilder.AppendLine("using System;");
                scriptBuilder.AppendLine("using System.Linq;");
                scriptBuilder.AppendLine("using System.Collections;");
                scriptBuilder.AppendLine("using System.Collections.Generic;");

                // Define each parameter as a variable
                foreach (var param in paramDict)
                {
                    if (param.Key != null && IsSafeIdentifier(param.Key))
                    {
                        // Directly get value from globals using GetValue method
                        scriptBuilder.AppendLine($"var {param.Key} = ((dynamic)globals)[\"" + param.Key + "\"];");
                    }
                }

                // Add the expression to evaluate
                scriptBuilder.AppendLine($"return {expression};");

                string script = scriptBuilder.ToString();
                Log.Debug($"Alternative script: {script}", option);

                return await CSharpScript.EvaluateAsync<object>(script, scriptOptions, globals);
            }
            catch (Exception fallbackEx)
            {
                Log.Debug($"Alternative evaluation failed: {fallbackEx.Message}", option);

                // Last resort: check if it's a simple property or collection access we can handle directly
                if (parameter != null)
                {
                    // Try simple indexer access as last resort using our optimized handlers
                    if (IsArrayIndexAccess(expression, out var arrName, out var idxKey))
                    {
                        if (TryGetPropertyValue(parameter, arrName, out var coll) && coll != null)
                        {
                            if (HandleCollectionAccess(coll, idxKey, out var result))
                            {
                                return result;
                            }
                        }
                    }
                }

                if (option.ThrowOnMissingParameter)
                    throw;

                return null;
            }
        }
    }

    /// <summary>
    /// Gets a friendly type name suitable for code generation
    /// </summary>
    private static string GetFriendlyTypeName(Type type)
    {
        if (type == typeof(string)) return "string";
        if (type == typeof(int)) return "int";
        if (type == typeof(long)) return "long";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(double)) return "double";
        if (type == typeof(decimal)) return "decimal";
        if (type == typeof(float)) return "float";
        if (type == typeof(char)) return "char";
        if (type == typeof(byte)) return "byte";
        if (type == typeof(object)) return "object";

        // For generic types, format them appropriately
        if (type.IsGenericType)
        {
            var genericArgs = type.GetGenericArguments();
            string baseName = type.Name.Split('`')[0];
            return $"{baseName}<{string.Join(", ", genericArgs.Select(GetFriendlyTypeName))}>";
        }

        return type.Name;
    }

    /// <summary>
    /// Gets the element type of an IEnumerable
    /// </summary>
    private static Type? GetElementType(IEnumerable? collection)
    {
        if (collection == null) return null;

        Type collectionType = collection.GetType();

        // For arrays, use element type
        if (collectionType.IsArray)
            return collectionType.GetElementType();

        // For generic collections, get the type argument
        if (collectionType.IsGenericType)
        {
            Type[] typeArgs = collectionType.GetGenericArguments();
            if (typeArgs.Length > 0)
                return typeArgs[0];
        }

        // Look through interfaces for IEnumerable<T>
        foreach (Type interfaceType in collectionType.GetInterfaces())
        {
            if (interfaceType.IsGenericType &&
                interfaceType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                return interfaceType.GetGenericArguments()[0];
            }
        }

        // Check first item as last resort
        foreach (var item in collection)
        {
            if (item != null)
                return item.GetType();
        }

        return typeof(object);
    }

    /// <summary>
    /// Prepare script options with commonly needed references and imports.
    /// </summary>
    private static ScriptOptions PrepareScriptOptions(DollarSignOption option)
    {
        var scriptOptions = ScriptOptions.Default
            .AddImports(
                "System",
                "System.Collections",
                "System.Collections.Generic",
                "System.Linq",
                "System.Text",
                "System.Threading.Tasks"
            )
            .AddReferences(
                typeof(System.Linq.Enumerable).Assembly,     // System.Core for LINQ
                typeof(List<>).Assembly,                    // System.Collections.Generic
                typeof(Dictionary<,>).Assembly,             // System.Collections.Generic  
                typeof(Microsoft.CSharp.RuntimeBinder.Binder).Assembly,  // Microsoft.CSharp for dynamic
                typeof(System.Dynamic.DynamicObject).Assembly, // System.Dynamic
                typeof(DollarSign).Assembly  // Add reference to this assembly
            );

        // Add additional namespaces and references if provided
        if (option.AdditionalNamespaces.Count > 0)
        {
            scriptOptions = scriptOptions.AddImports(option.AdditionalNamespaces);
        }

        if (option.AdditionalAssemblies.Count > 0)
        {
            scriptOptions = scriptOptions.AddReferences(option.AdditionalAssemblies);
        }

        return scriptOptions;
    }

    /// <summary>
    /// Extracts all properties from an object to a dictionary.
    /// </summary>
    private static Dictionary<string, object?> ExtractPropertiesToDictionary(object obj)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (obj is IDictionary<string, object?> dict)
        {
            // If it's already a dictionary, use it directly
            foreach (var pair in dict)
            {
                result[pair.Key] = pair.Value;
            }
        }
        else if (obj is IDictionary genericDict)
        {
            // Handle generic dictionaries
            foreach (DictionaryEntry entry in genericDict)
            {
                if (entry.Key is string key)
                {
                    result[key] = entry.Value;
                }
            }
        }
        else
        {
            // For objects, extract all properties using reflection
            var properties = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                try
                {
                    var value = prop.GetValue(obj);
                    result[prop.Name] = value;
                }
                catch
                {
                    // Skip properties that fail to be read
                    result[prop.Name] = null;
                }
            }
        }

        return result;
    }
}