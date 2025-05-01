using System.Collections;
using System.Collections.ObjectModel;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DollarSignEngine;

/// <summary>
/// Contains global types for script evaluation in the DollarSign class.
/// </summary>
public static partial class DollarSign
{
    /// <summary>
    /// ScriptGlobals class provides a robust implementation for property access 
    /// with complete type information preservation, designed to be used with Roslyn scripts.
    /// </summary>
    public class ScriptGlobals
    {
        private readonly Dictionary<string, object?> _values;

        public ScriptGlobals(Dictionary<string, object?> values)
        {
            _values = values;
        }

        /// <summary>
        /// Standard indexer for direct access
        /// </summary>
        public object? this[string name] => _values.TryGetValue(name, out var value) ? value : null;

        /// <summary>
        /// Dynamically exposes all properties directly to Roslyn scripts
        /// </summary>
        public dynamic GetDynamic()
        {
            return new DynamicProperties(_values);
        }

        /// <summary>
        /// Gets a strongly typed value from the dictionary or null if not found
        /// </summary>
        public T? GetValue<T>(string name)
        {
            if (_values.TryGetValue(name, out var value) && value != null)
            {
                if (value is T typedValue)
                {
                    return typedValue;
                }

                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    // Conversion failed
                    return default;
                }
            }

            return default;
        }

        /// <summary>
        /// Gets a collection as a properly typed IEnumerable<T>
        /// </summary>
        public IEnumerable<T>? GetCollection<T>(string name)
        {
            if (_values.TryGetValue(name, out var value) && value != null)
            {
                // If already the right type, return directly
                if (value is IEnumerable<T> typedCollection)
                {
                    return typedCollection;
                }

                // If it's a general collection, try to convert it
                if (value is IEnumerable collection)
                {
                    var result = new List<T>();

                    foreach (var item in collection)
                    {
                        if (item is T typedItem)
                        {
                            result.Add(typedItem);
                        }
                        else if (item != null)
                        {
                            try
                            {
                                var convertedItem = (T)Convert.ChangeType(item, typeof(T));
                                result.Add(convertedItem);
                            }
                            catch
                            {
                                // Skip items that can't be converted
                            }
                        }
                    }

                    return result;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Dynamic property accessor that exposes values to script engine
    /// </summary>
    public class DynamicProperties : DynamicObject
    {
        private readonly Dictionary<string, object?> _values;

        public DynamicProperties(Dictionary<string, object?> values)
        {
            _values = values;
        }

        // This is key for direct access in script
        public override bool TryGetMember(GetMemberBinder binder, out object? result)
        {
            if (_values.TryGetValue(binder.Name, out result))
            {
                return true;
            }

            result = null;
            return true; // Return true to prevent exceptions for missing properties
        }

        // Enable property indexing
        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object? result)
        {
            result = null;

            if (indexes.Length != 1)
                return false;

            var index = indexes[0];

            // Find collection or dictionary in values
            foreach (var pair in _values)
            {
                var value = pair.Value;
                if (value == null) continue;

                // Handle arrays
                if (value is Array array && index is int intIndex)
                {
                    if (intIndex >= 0 && intIndex < array.Length)
                    {
                        result = array.GetValue(intIndex);
                        return true;
                    }
                }

                // Handle IList collections
                if (value is IList list && index is int listIndex)
                {
                    if (listIndex >= 0 && listIndex < list.Count)
                    {
                        result = list[listIndex];
                        return true;
                    }
                }

                // Handle dictionaries
                if (value is IDictionary dict && dict.Contains(index))
                {
                    result = dict[index];
                    return true;
                }
            }

            return false;
        }

        // Enable method invocation directly from script
        public override bool TryInvokeMember(InvokeMemberBinder binder, object?[]? args, out object? result)
        {
            // Try to get the method from any value 
            foreach (var value in _values.Values)
            {
                if (value == null) continue;

                try
                {
                    var method = value.GetType().GetMethod(binder.Name,
                        BindingFlags.Public | BindingFlags.Instance);

                    if (method != null)
                    {
                        result = method.Invoke(value, args);
                        return true;
                    }
                }
                catch
                {
                    // Try next value
                }
            }

            // Handle LINQ methods on collections
            if (args != null && args.Length > 0)
            {
                foreach (var value in _values.Values)
                {
                    if (value is IEnumerable collection)
                    {
                        try
                        {
                            // Try to find appropriate extension method 
                            var method = typeof(Enumerable).GetMethods()
                                .FirstOrDefault(m => m.Name == binder.Name);

                            if (method != null)
                            {
                                if (method.IsGenericMethod)
                                {
                                    Type elementType = GetElementType(collection) ?? typeof(object);
                                    var genericMethod = method.MakeGenericMethod(elementType);

                                    var allArgs = new object[args.Length + 1];
                                    allArgs[0] = collection;
                                    Array.Copy(args, 0, allArgs, 1, args.Length);

                                    result = genericMethod.Invoke(null, allArgs);
                                    return true;
                                }
                            }
                        }
                        catch
                        {
                            // Try next value
                        }
                    }
                }
            }

            result = null;
            return false;
        }
    }
}