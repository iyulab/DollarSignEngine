using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace DollarSignEngine.Internals;

/// <summary>
/// Enhanced AssemblyReferenceHelper with dynamic assembly discovery from variables.
/// </summary>
internal static class AssemblyReferenceHelper
{
    private static readonly ConcurrentDictionary<string, MetadataReference> _assemblyReferenceCache =
        new ConcurrentDictionary<string, MetadataReference>();

    private static readonly ConcurrentSet<Assembly> _discoveredAssemblies = new();

    // Core assemblies that are always included
    private static readonly HashSet<string> _coreAssemblyNames = new()
    {
        typeof(object).Assembly.GetName().Name!,
        typeof(System.Linq.Enumerable).Assembly.GetName().Name!,
        typeof(System.Collections.Generic.List<>).Assembly.GetName().Name!,
        typeof(System.Text.RegularExpressions.Regex).Assembly.GetName().Name!,
        typeof(Uri).Assembly.GetName().Name!,
        typeof(Microsoft.CSharp.RuntimeBinder.Binder).Assembly.GetName().Name!,
        typeof(System.Dynamic.IDynamicMetaObjectProvider).Assembly.GetName().Name!,
    };

    /// <summary>
    /// Gets metadata references with dynamic assembly discovery from context variables.
    /// </summary>
    public static IEnumerable<MetadataReference> GetReferences(
        object? contextObject,
        IEnumerable<Assembly>? additionalAssemblies = null)
    {
        var assemblies = new HashSet<Assembly>();
        var result = new List<MetadataReference>();

        Logger.Debug("[AssemblyReferenceHelper] Starting assembly discovery...");

        // Add core assemblies
        AddCoreAssemblies(assemblies);
        Logger.Debug($"[AssemblyReferenceHelper] Added {assemblies.Count} core assemblies");

        // Dynamically discover assemblies from variables
        if (contextObject != null)
        {
            var beforeCount = assemblies.Count;
            DiscoverAssembliesFromVariables(contextObject, assemblies);
            Logger.Debug($"[AssemblyReferenceHelper] Discovered {assemblies.Count - beforeCount} assemblies from variables");

            // Log discovered assemblies for debugging
            foreach (var asm in assemblies.Skip(beforeCount))
            {
                Logger.Debug($"[AssemblyReferenceHelper] Discovered assembly: {asm.FullName}");
            }
        }

        // Add additional assemblies
        if (additionalAssemblies != null)
        {
            var beforeCount = assemblies.Count;
            foreach (var asm in additionalAssemblies)
            {
                AddValidAssembly(assemblies, asm);
            }
            Logger.Debug($"[AssemblyReferenceHelper] Added {assemblies.Count - beforeCount} additional assemblies");
        }

        // Convert to metadata references
        foreach (var assembly in assemblies)
        {
            try
            {
                result.Add(GetOrCreateMetadataReference(assembly));
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to create metadata reference for {assembly.FullName}: {ex.Message}");
            }
        }

        Logger.Debug($"[AssemblyReferenceHelper] Total metadata references created: {result.Count}");
        return result;
    }

    /// <summary>
    /// Discovers assemblies from variable types recursively.
    /// </summary>
    private static void DiscoverAssembliesFromVariables(object variables, HashSet<Assembly> assemblies)
    {
        if (variables == null) return;

        var visited = new HashSet<object>();

        // First, handle the root object and its immediate types
        DiscoverAssembliesFromType(variables.GetType(), assemblies);

        // Then recursively discover from the object graph
        DiscoverAssembliesRecursive(variables, assemblies, visited, maxDepth: 4);
    }

    /// <summary>
    /// Discovers assemblies from a specific type and its dependencies.
    /// </summary>
    private static void DiscoverAssembliesFromType(Type type, HashSet<Assembly> assemblies)
    {
        if (type == null) return;

        Logger.Debug($"[AssemblyReferenceHelper] Analyzing type: {type.FullName ?? type.Name}");
        AddValidAssembly(assemblies, type.Assembly);

        // Handle generic types - this is crucial for List<T>, Dictionary<T,U> etc.
        if (type.IsGenericType)
        {
            // Add assembly of the generic type definition
            AddValidAssembly(assemblies, type.GetGenericTypeDefinition().Assembly);

            // Add assemblies of all generic arguments
            foreach (var genericArg in type.GetGenericArguments())
            {
                Logger.Debug($"[AssemblyReferenceHelper] Found generic argument: {genericArg.FullName ?? genericArg.Name}");
                DiscoverAssembliesFromType(genericArg, assemblies);
            }
        }

        // Handle array types
        if (type.IsArray)
        {
            var elementType = type.GetElementType();
            if (elementType != null)
            {
                Logger.Debug($"[AssemblyReferenceHelper] Found array element type: {elementType.FullName ?? elementType.Name}");
                DiscoverAssembliesFromType(elementType, assemblies);
            }
        }

        // Handle nested types
        if (type.IsNested)
        {
            Logger.Debug($"[AssemblyReferenceHelper] Found nested type, analyzing declaring type: {type.DeclaringType?.FullName}");
            DiscoverAssembliesFromType(type.DeclaringType!, assemblies);
        }
    }

    /// <summary>
    /// Recursively discovers assemblies from object graph.
    /// </summary>
    private static void DiscoverAssembliesRecursive(object obj, HashSet<Assembly> assemblies,
        HashSet<object> visited, int maxDepth, int currentDepth = 0)
    {
        if (obj == null || currentDepth >= maxDepth || visited.Contains(obj)) return;

        visited.Add(obj);
        var type = obj.GetType();

        // Add assembly of the object type and its dependencies
        DiscoverAssembliesFromType(type, assemblies);

        // Handle collections and dictionaries
        if (obj is IDictionary<string, object?> dictNullable)
        {
            foreach (var kvp in dictNullable.Take(5))
            {
                if (kvp.Value != null)
                {
                    // Discover assemblies from the value's type
                    DiscoverAssembliesFromType(kvp.Value.GetType(), assemblies);
                    DiscoverAssembliesRecursive(kvp.Value, assemblies, visited, maxDepth, currentDepth + 1);
                }
            }
        }
        else if (obj is IDictionary<string, object> dict)
        {
            foreach (var kvp in dict.Take(5))
            {
                if (kvp.Value != null)
                {
                    // Discover assemblies from the value's type
                    DiscoverAssembliesFromType(kvp.Value.GetType(), assemblies);
                    DiscoverAssembliesRecursive(kvp.Value, assemblies, visited, maxDepth, currentDepth + 1);
                }
            }
        }
        else if (obj is IEnumerable enumerable && !(obj is string))
        {
            var count = 0;
            foreach (var item in enumerable)
            {
                if (item != null && count++ < 5) // Limit enumeration
                {
                    // Discover assemblies from the item's type
                    DiscoverAssembliesFromType(item.GetType(), assemblies);
                    DiscoverAssembliesRecursive(item, assemblies, visited, maxDepth, currentDepth + 1);
                }
                else break;
            }
        }
        else
        {
            // For regular objects, check public properties
            if (!IsPrimitiveOrSimpleType(type))
            {
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                    .Take(10); // Limit properties to check

                foreach (var prop in properties)
                {
                    try
                    {
                        // Discover assemblies from property type
                        DiscoverAssembliesFromType(prop.PropertyType, assemblies);

                        var propValue = prop.GetValue(obj);
                        if (propValue != null && !IsPrimitiveOrSimpleType(propValue.GetType()))
                        {
                            DiscoverAssembliesRecursive(propValue, assemblies, visited, maxDepth, currentDepth + 1);
                        }
                    }
                    catch
                    {
                        // Ignore property access exceptions
                    }
                }
            }
        }
    }

    /// <summary>
    /// Checks if type is primitive or simple type that doesn't need deep inspection.
    /// </summary>
    private static bool IsPrimitiveOrSimpleType(Type type)
    {
        return type.IsPrimitive ||
               type == typeof(string) ||
               type == typeof(decimal) ||
               type == typeof(DateTime) ||
               type == typeof(DateTimeOffset) ||
               type == typeof(TimeSpan) ||
               type == typeof(Guid) ||
               type.IsEnum ||
               (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>));
    }

    /// <summary>
    /// Adds core assemblies that are always needed.
    /// </summary>
    private static void AddCoreAssemblies(HashSet<Assembly> assemblies)
    {
        // Add core runtime assemblies
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (_coreAssemblyNames.Contains(asm.GetName().Name!))
            {
                AddValidAssembly(assemblies, asm);
            }
        }

        // Add essential assemblies
        var essentialAssemblies = new[]
        {
            "System.Core", "Microsoft.CSharp", "System.Dynamic.Runtime",
            "System.Linq.Expressions", "System.ComponentModel.Primitives"
        };

        foreach (var asmName in essentialAssemblies)
        {
            try
            {
                AddValidAssembly(assemblies, Assembly.Load(asmName));
            }
            catch (Exception ex)
            {
                Logger.Debug($"Could not load essential assembly {asmName}: {ex.Message}");
            }
        }

        // Add our own assemblies
        AddValidAssembly(assemblies, typeof(ExpressionEvaluator).Assembly);
        AddValidAssembly(assemblies, typeof(DollarSign).Assembly);
    }

    /// <summary>
    /// Gets or creates a cached metadata reference for an assembly.
    /// </summary>
    private static MetadataReference GetOrCreateMetadataReference(Assembly assembly)
    {
        if (string.IsNullOrEmpty(assembly.Location))
        {
            throw new ArgumentException($"Assembly {assembly.FullName} has no location");
        }

        return _assemblyReferenceCache.GetOrAdd(assembly.Location, location =>
        {
            try
            {
                return MetadataReference.CreateFromFile(location);
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to create metadata reference for {location}: {ex.Message}");
                throw;
            }
        });
    }

    /// <summary>
    /// Adds an assembly to the collection if it's valid.
    /// </summary>
    private static void AddValidAssembly(HashSet<Assembly> assemblies, Assembly? assembly)
    {
        if (assembly == null ||
            assembly.IsDynamic ||
            string.IsNullOrEmpty(assembly.Location) ||
            !File.Exists(assembly.Location))
        {
            if (assembly != null)
            {
                Logger.Debug($"[AssemblyReferenceHelper] Skipping invalid assembly: {assembly.FullName} (IsDynamic: {assembly.IsDynamic}, Location: {assembly.Location})");
            }
            return;
        }

        if (assemblies.Add(assembly))
        {
            Logger.Debug($"[AssemblyReferenceHelper] Added assembly: {assembly.FullName}");
            _discoveredAssemblies.Add(assembly);
        }
    }

    /// <summary>
    /// Clears the assembly reference cache.
    /// </summary>
    public static void ClearCache()
    {
        _assemblyReferenceCache.Clear();
        _discoveredAssemblies.Clear();
        Logger.Debug("[AssemblyReferenceHelper.ClearCache] Assembly caches cleared.");
    }

    /// <summary>
    /// Gets statistics about discovered assemblies.
    /// </summary>
    public static (int TotalCached, int TotalDiscovered) GetStatistics()
    {
        return (_assemblyReferenceCache.Count, _discoveredAssemblies.Count);
    }
}

/// <summary>
/// Thread-safe hash set for concurrent assembly tracking.
/// </summary>
internal class ConcurrentSet<T> : IEnumerable<T> where T : notnull
{
    private readonly ConcurrentDictionary<T, byte> _dict = new();

    public bool Add(T item) => _dict.TryAdd(item, 0);
    public bool Contains(T item) => _dict.ContainsKey(item);
    public void Clear() => _dict.Clear();
    public int Count => _dict.Count;

    public IEnumerator<T> GetEnumerator() => _dict.Keys.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}