using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace DollarSignEngine;

/// <summary>
/// Provides functionality to dynamically evaluate C# expressions using interpolation strings and parameters.
/// Core functionality including the main evaluation methods.
/// </summary>
public static partial class DollarSign
{
    /// <summary>
    /// Asynchronously evaluates a given C# expression as a string and returns the result.
    /// The method accepts an expression, optionally with parameters, compiles it into a runnable script, and evaluates it.
    /// This allows for the dynamic evaluation of expressions with embedded variables and complex logic.
    /// If the compilation or execution fails, it throws a DollarSignEngineException with details of the error.
    /// </summary>
    public static async Task<string> EvalAsync(string expression, object? parameter = null, DollarSignOption? option = null)
    {
        try
        {
            expandoCounter = 0;

            var options = ScriptOptions.Default
                .WithImports("System", "System.Collections.Generic", "System.Linq", "System.Dynamic")
                .AddReferences(
                    Assembly.Load("mscorlib"),
                    Assembly.Load("System"),
                    Assembly.Load("System.Core"),
                    Assembly.Load("Microsoft.CSharp")
                );

            // 추가 옵션 적용
            if (option != null)
            {
                if (option.AdditionalNamespaces.Count > 0)
                {
                    options = options.WithImports(option.AdditionalNamespaces);
                }

                if (option.AdditionalAssemblies.Count > 0)
                {
                    var assemblies = option.AdditionalAssemblies
                        .Select(name => Assembly.Load(name))
                        .ToArray();
                    options = options.AddReferences(assemblies);
                }

                if (option.EnableDebugLogging)
                {
                    Debug.WriteLine($"[DollarSign] Expression: {expression}");
                }
            }

            // Convert parameter to dictionary or keep as is based on its type
            IDictionary<string, object?>? paramDict = null;
            if (parameter != null)
            {
                paramDict = ConvertParameterToDictionary(parameter);
            }

            var script = BuildCsScript(expression, paramDict);
#if DEBUG
            Debug.WriteLine("[Logs]");
            Debug.WriteLine($"expression: {expression}");
            Debug.WriteLine($"script: {script}");
#endif
            var result = await CSharpScript.EvaluateAsync<string>(script, options);
            return result;
        }
        catch (CompilationErrorException compilationErrorException)
        {
            throw new DollarSignEngineException($"CompilationError: {compilationErrorException.Message}", compilationErrorException);
        }
        catch (Exception e)
        {
            throw new DollarSignEngineException($"Error: {e.Message}", e);
        }
    }

    /// <summary>
    /// Builds a C# script from the expression and parameters.
    /// </summary>
    internal static string BuildCsScript(string expression, IDictionary<string, object?>? parameters)
    {
        var declarations = new StringBuilder();
        var getFunctions = new StringBuilder();
        var helperFunctions = new StringBuilder();

        // Add helper function for collection access
        helperFunctions.AppendLine(@"
string GetCollectionProperty(object collection, string propertyName)
{
    if (collection == null) return ""null"";
    
    // Handle common collection types
    if (collection is System.Collections.ICollection coll)
    {
        if (propertyName == ""Count"")
            return coll.Count.ToString();
    }
    
    // Use reflection for any other property access
    var prop = collection.GetType().GetProperty(propertyName);
    if (prop != null)
    {
        var value = prop.GetValue(collection);
        return value?.ToString() ?? ""null"";
    }
    
    return $""Property {propertyName} not found"";
}
");

        if (parameters != null)
        {
            var scriptPrams = new Dictionary<string, string>();
            foreach (var param in parameters)
            {
                if (param.Value == null) continue;
                var convertedValue = ConvertValue(param.Value, scriptPrams);

                declarations.AppendLine($"var {param.Key} = {convertedValue};");
                if (scriptPrams.Count > 0)
                {
                    foreach (var p in scriptPrams)
                    {
                        getFunctions.Append(p.Value);
                    }
                }
            }
        }

        // Process the expression to handle collection access
        var processedExpression = ProcessCollectionAccess(expression);

        var scriptBody = processedExpression.StartsWith('$') ? processedExpression : $"$\"{processedExpression}\"";
        if (!scriptBody.EndsWith(';'))
        {
            scriptBody += ";";
        }

        return helperFunctions.ToString() + getFunctions.ToString() + declarations.ToString() + $"return {scriptBody}";
    }

    /// <summary>
    /// Processes the expression to handle collection access patterns and replace them with safe access methods.
    /// </summary>
    private static string ProcessCollectionAccess(string expression)
    {
        // This is a basic implementation that would need more sophisticated parsing for all cases
        // Here we're just looking for simple collection.Count patterns

        // Simple regex-like replacement (without using actual regex for simplicity)
        // Look for patterns like {items.Count} and replace with {GetCollectionProperty(items, "Count")}

        // For a complete solution, this would need proper parsing/tokenizing of the expression

        // This is a simplified version - in a real implementation, you would use a proper parser
        // to handle all the edge cases and nested expressions

        int startIdx = 0;
        var result = new StringBuilder();

        while (true)
        {
            // Find opening brace
            int openBrace = expression.IndexOf('{', startIdx);
            if (openBrace == -1) break;

            // Find closing brace
            int closeBrace = expression.IndexOf('}', openBrace);
            if (closeBrace == -1) break;

            // Add everything up to the opening brace
            result.Append(expression.Substring(startIdx, openBrace - startIdx + 1));

            // Extract the content inside braces
            string content = expression.Substring(openBrace + 1, closeBrace - openBrace - 1);

            // Check if it's a collection property access pattern (e.g., "items.Count")
            // This is a very basic check and would need to be more sophisticated for a real implementation
            if (content.Contains(".Count") && !content.Contains('?') && !content.Contains(':'))
            {
                var parts = content.Split('.');
                if (parts.Length == 2 && parts[1] == "Count")
                {
                    // Replace with GetCollectionProperty call
                    result.Append($"GetCollectionProperty({parts[0]}, \"Count\")");
                }
                else
                {
                    // Keep as is
                    result.Append(content);
                }
            }
            else
            {
                // Keep as is
                result.Append(content);
            }

            // Add closing brace
            result.Append('}');

            // Move to next position
            startIdx = closeBrace + 1;
        }

        // Add the rest of the expression
        if (startIdx < expression.Length)
        {
            result.Append(expression[startIdx..]);
        }

        return result.ToString();
    }
}