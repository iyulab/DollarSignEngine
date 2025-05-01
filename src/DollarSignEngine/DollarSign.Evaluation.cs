using System.Collections;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace DollarSignEngine;

/// <summary>
/// Contains expression evaluation functionality for the DollarSign class.
/// </summary>
public static partial class DollarSign
{
    private static async Task<object?> EvaluateExpressionAsync(string expression, object? parameter, DollarSignOption option)
    {
        Log.Debug($"Evaluating expression: {expression}", option);
        Log.Debug($"With parameter type: {parameter?.GetType().Name ?? "null"}", option);

        // Check if the expression is a simple method call (e.g., "Hello()")
        bool isMethodCall = Regex.IsMatch(expression, @"^[a-zA-Z_][a-zA-Z0-9_]*\s*\(\s*\)$");

        // Try variable resolver callback if provided
        if (option.VariableResolver != null && option.PreferCallbackResolution)
        {
            try
            {
                string? resolverExpression = expression;
                if (IsLinqExpression(expression) && TryGetCollectionTarget(expression, out var collectionName))
                {
                    resolverExpression = collectionName;
                }
                else if (IsSimpleIdentifier(expression) || isMethodCall)
                {
                    resolverExpression = expression;
                }
                else if (IsPropertyChain(expression, out var objName, out _))
                {
                    resolverExpression = objName;
                }
                else if (IsArrayIndexAccess(expression, out var arrayName, out _))
                {
                    resolverExpression = arrayName;
                }

                var value = option.VariableResolver(resolverExpression, parameter);
                if (value != null)
                {
                    Log.Debug($"Callback resolver succeeded for {resolverExpression}: {value}", option);

                    if (resolverExpression == expression)
                    {
                        return value;
                    }

                    if (IsLinqExpression(expression) && TryGetCollectionTarget(expression, out var targetName) && targetName == resolverExpression)
                    {
                        if (TryHandleLinqOperationDirectly(expression, value, out var linqResult))
                        {
                            Log.Debug($"Direct LINQ operation succeeded: {linqResult}", option);
                            return linqResult;
                        }
                    }

                    var paramDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    { resolverExpression, value }
                };

                    if (parameter != null)
                    {
                        var additionalParams = ExtractPropertiesToDictionary(parameter);
                        foreach (var kvp in additionalParams)
                        {
                            paramDict.TryAdd(kvp.Key, kvp.Value);
                        }
                    }

                    var globals = new ScriptGlobals(paramDict);
                    Log.Debug($"Using ScriptGlobals with variable: {resolverExpression}", option);
                    return await CompileAndEvaluateExpressionAsync(expression, globals, option);
                }
            }
            catch (Exception ex)
            {
                Log.Debug($"Callback resolver failed: {ex.Message}", option);
                if (option.StrictParameterAccess)
                    throw;
            }
        }

        if (parameter != null && option.OptimizeCollectionAccess)
        {
            if (IsSimpleIdentifier(expression))
            {
                if (TryGetPropertyValue(parameter, expression, out var value))
                {
                    Log.Debug($"Direct property access succeeded: {value}", option);
                    return value;
                }
            }

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

            if (IsLinqExpression(expression))
            {
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

            // Handle method calls directly
            if (isMethodCall)
            {
                string methodName = expression.Substring(0, expression.IndexOf('('));
                try
                {
                    var methodInfo = parameter.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
                    if (methodInfo != null)
                    {
                        var result = methodInfo.Invoke(parameter, null);
                        Log.Debug($"Direct method invocation succeeded: {result}", option);
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug($"Direct method invocation failed: {ex.Message}", option);
                }
            }
        }

        // Use Roslyn script evaluation with adjusted expression for method calls
        try
        {
            Log.Debug($"Using Roslyn evaluation for: {expression}", option);
            string scriptExpression = isMethodCall ? $"__parameter.{expression}" : expression;
            return await CompileAndEvaluateExpressionAsync(scriptExpression, parameter, option);
        }
        catch (Exception ex)
        {
            Log.Debug($"Expression evaluation error: {ex.Message}", option);

            if (option.ThrowOnMissingParameter)
                throw;

            return null;
        }
    }

    private static async Task<object?> CompileAndEvaluateExpressionAsync(string expression, object? parameter, DollarSignOption option)
    {
        // Set up script options with necessary references and imports
        var scriptOptions = PrepareScriptOptions(option);

        // If no parameter is provided, just evaluate the expression directly
        if (parameter == null)
        {
            return await CSharpScript.EvaluateAsync<object>(expression, scriptOptions);
        }

        // If parameter is already a ScriptGlobals, use it directly
        if (parameter is ScriptGlobals globals1)
        {
            try
            {
                return await CSharpScript.EvaluateAsync<object>(expression, scriptOptions, globals1);
            }
            catch (Exception ex)
            {
                Log.Debug($"Direct evaluation with ScriptGlobals failed: {ex.Message}", option);
                throw;
            }
        }

        // Create ScriptGlobals with parameter properties
        var paramDict = ExtractPropertiesToDictionary(parameter);
        paramDict["__parameter"] = parameter; // Add parameter for method access
        var globals2 = new ScriptGlobals(paramDict);

        try
        {
            // Direct approach: create a script with parameter access
            string directScript = expression;

            // Try direct evaluation first with globals object accessible
            return await CSharpScript.EvaluateAsync<object>(directScript, scriptOptions, globals2);
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

                // Define a unique globals variable for the script
                scriptBuilder.AppendLine($"var _scriptGlobals = (dynamic)@globals;");

                // Define the parameter object for method access
                scriptBuilder.AppendLine($"var __parameter = _scriptGlobals[\"__parameter\"];");

                // Define each property as a variable
                foreach (var param in paramDict)
                {
                    if (param.Key != null && IsSafeIdentifier(param.Key) && param.Key != "__parameter")
                    {
                        scriptBuilder.AppendLine($"var {param.Key} = _scriptGlobals[\"{param.Key}\"];");
                    }
                }

                // Add the expression to evaluate
                scriptBuilder.AppendLine($"return {expression};");

                string script = scriptBuilder.ToString();
                Log.Debug($"Alternative script: {script}", option);

                // Evaluate with the globals object passed as a parameter
                return await CSharpScript.EvaluateAsync<object>(script, scriptOptions, globals2);
            }
            catch (Exception fallbackEx)
            {
                Log.Debug($"Alternative evaluation failed: {fallbackEx.Message}", option);

                // Last resort: check if it's a simple property or collection access
                if (parameter != null)
                {
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