using DynamicExpresso;

namespace DollarSignEngine.Evaluation;

internal partial class ExpressionEvaluator
{
    /// <summary>
    /// Creates and configures an enhanced interpreter instance.
    /// </summary>
    private Interpreter CreateEnhancedInterpreter(DollarSignOptions option)
    {
        var interpreter = new Interpreter(InterpreterOptions.DefaultCaseInsensitive)
            .EnableReflection()
            .Reference(typeof(Enumerable))
            .Reference(typeof(List<>))
            .Reference(typeof(Dictionary<,>))
            .Reference(typeof(DateTime))
            .Reference(typeof(string))
            .Reference(typeof(Regex))
            .Reference(typeof(Queryable))  // Add Queryable for better LINQ support
            .Reference(typeof(IQueryable<>))
            .Reference(typeof(IEnumerable<>))
            .Reference(typeof(ICollection<>));

        // Reference essential namespaces through their types
        interpreter = interpreter
            .Reference(typeof(Enumerable))  // System.Linq
            .Reference(typeof(List<>))      // System.Collections.Generic
            .Reference(typeof(Path))        // System.IO
            .Reference(typeof(Regex))       // System.Text.RegularExpressions
            .Reference(typeof(StringComparer))  // System
            .Reference(typeof(Convert))     // System
            .Reference(typeof(Math))        // System
            .Reference(typeof(Guid))        // System
            .Reference(typeof(StringBuilder));  // System.Text

        // Add additional assemblies from options
        foreach (var ns in option.AdditionalNamespaces)
        {
            try
            {
                // Try to load as an assembly
                Assembly? assembly = null;

                try
                {
                    assembly = Assembly.Load(ns);
                }
                catch
                {
                    // If it's not a valid assembly, log and continue
                    Log.Debug($"Could not load {ns} as an assembly", option);
                    continue;
                }

                if (assembly != null)
                {
                    // Reference key exported types from the assembly
                    var exportedTypes = assembly.GetExportedTypes()
                        .Where(t => !t.IsGenericTypeDefinition)  // Skip open generic types
                        .Where(t => !t.IsAbstract || t.IsSealed) // Include only concrete or static types
                        .Take(100);  // Limit to avoid referencing too many types

                    foreach (var exportedType in exportedTypes)
                    {
                        try
                        {
                            interpreter = interpreter.Reference(exportedType);
                        }
                        catch
                        {
                            // Skip types that can't be referenced
                        }
                    }

                    Log.Debug($"Successfully referenced types from assembly {ns}", option);
                }
            }
            catch (Exception ex)
            {
                Log.Debug($"Error processing namespace or assembly {ns}: {ex.Message}", option);
            }
        }

        return interpreter;
    }

    /// <summary>
    /// Extracts parameters from an object for use in expression evaluation.
    /// </summary>
    private Dictionary<string, object?> ExtractParameters(object? parameter)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (parameter == null)
            return result;

        // Add the parameter itself with a special name
        result["param"] = parameter;

        // For dictionary types, extract key-value pairs
        if (parameter is IDictionary<string, object?> dict)
        {
            foreach (var pair in dict)
            {
                result[pair.Key] = pair.Value;
            }
            return result;
        }
        else if (parameter is IDictionary nonGenericDict)
        {
            foreach (DictionaryEntry entry in nonGenericDict)
            {
                if (entry.Key is string key)
                {
                    result[key] = entry.Value;
                }
            }
            return result;
        }

        // For anonymous types and regular objects, extract properties
        var properties = parameter.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in properties)
        {
            try
            {
                var value = prop.GetValue(parameter);
                result[prop.Name] = value;
            }
            catch
            {
                // Skip properties that can't be read
            }
        }

        // Extract methods as well (for no-parameter methods)
        var methods = parameter.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Length == 0 && m.ReturnType != typeof(void) && !m.IsSpecialName);

        foreach (var method in methods)
        {
            try
            {
                // For no-parameter methods, create a lambda that will invoke the method when called
                result[method.Name] = new Func<object?>(() => method.Invoke(parameter, null));
            }
            catch
            {
                // Skip methods that can't be processed
            }
        }

        return result;
    }
}