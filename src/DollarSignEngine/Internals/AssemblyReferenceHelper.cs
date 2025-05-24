using System.Collections.Concurrent;

namespace DollarSignEngine.Internals;

/// <summary>
/// Helper class for managing assembly references for script compilation.
/// Fixed deadlock issues with simplified caching strategy and added runtime binder support.
/// </summary>
internal static class AssemblyReferenceHelper
{
    // Simple cache without complex locking - use ConcurrentDictionary for thread safety
    private static readonly ConcurrentDictionary<string, MetadataReference> _assemblyReferenceCache =
        new ConcurrentDictionary<string, MetadataReference>();

    // Core assemblies that are always included
    private static readonly HashSet<string> _coreAssemblyNames = new()
    {
        typeof(object).Assembly.GetName().Name!,                // System.Private.CoreLib
        typeof(System.Linq.Enumerable).Assembly.GetName().Name!, // System.Linq
        typeof(System.Collections.Generic.List<>).Assembly.GetName().Name!, // System.Collections
        typeof(System.Text.RegularExpressions.Regex).Assembly.GetName().Name!, // System.Text.RegularExpressions
        typeof(Uri).Assembly.GetName().Name!, // System.Private.Uri
        typeof(Microsoft.CSharp.RuntimeBinder.Binder).Assembly.GetName().Name!, // Microsoft.CSharp
        typeof(System.Dynamic.IDynamicMetaObjectProvider).Assembly.GetName().Name!, // System.Dynamic.Runtime
    };

    /// <summary>
    /// Gets metadata references needed for script compilation.
    /// </summary>
    public static IEnumerable<MetadataReference> GetReferences(
        object? contextObject,
        IEnumerable<Assembly>? additionalAssembliesFromTypes = null)
    {
        var assemblies = new HashSet<Assembly>();
        var result = new List<MetadataReference>();

        // Add core assemblies
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (_coreAssemblyNames.Contains(asm.GetName().Name!))
            {
                AddValidAssembly(assemblies, asm);
            }
        }

        // Explicitly add critical assemblies for dynamic support
        try
        {
            // System.Core for LINQ
            AddValidAssembly(assemblies, Assembly.Load("System.Core"));
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to load System.Core assembly: {ex.Message}");
        }

        try
        {
            // Microsoft.CSharp for dynamic runtime binding
            AddValidAssembly(assemblies, Assembly.Load("Microsoft.CSharp"));
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to load Microsoft.CSharp assembly: {ex.Message}");
        }

        try
        {
            // System.Dynamic.Runtime for dynamic objects
            AddValidAssembly(assemblies, Assembly.Load("System.Dynamic.Runtime"));
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to load System.Dynamic.Runtime assembly: {ex.Message}");
        }

        // Add runtime assemblies that might be needed for dynamic operations
        try
        {
            AddValidAssembly(assemblies, typeof(System.Runtime.CompilerServices.DynamicAttribute).Assembly);
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to load runtime assembly: {ex.Message}");
        }

        // Add our own assemblies
        AddValidAssembly(assemblies, typeof(ExpressionEvaluator).Assembly);
        AddValidAssembly(assemblies, typeof(DollarSign).Assembly);

        // Add context object assemblies
        if (contextObject != null)
        {
            AddValidAssembly(assemblies, contextObject.GetType().Assembly);

            // Add assemblies from generic type arguments
            Type contextType = contextObject.GetType();
            if (contextType.IsGenericType)
            {
                foreach (Type argType in contextType.GetGenericArguments())
                {
                    AddValidAssembly(assemblies, argType.Assembly);
                }
            }
        }

        // Add additional assemblies
        if (additionalAssembliesFromTypes != null)
        {
            foreach (var asm in additionalAssembliesFromTypes)
            {
                AddValidAssembly(assemblies, asm);
            }
        }

        // Convert assemblies to metadata references
        foreach (var assembly in assemblies)
        {
            try
            {
                result.Add(GetOrCreateMetadataReference(assembly));
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to create metadata reference for assembly {assembly.FullName}: {ex.Message}");
            }
        }

        return result;
    }

    /// <summary>
    /// Gets or creates a cached metadata reference for an assembly.
    /// Uses simple concurrent dictionary to avoid deadlock issues.
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
        if (assembly == null) return;

        if (!assembly.IsDynamic &&
            !string.IsNullOrEmpty(assembly.Location) &&
            File.Exists(assembly.Location))
        {
            assemblies.Add(assembly);
        }
    }

    /// <summary>
    /// Clears the assembly reference cache.
    /// </summary>
    public static void ClearCache()
    {
        _assemblyReferenceCache.Clear();
        Logger.Debug("[AssemblyReferenceHelper.ClearCache] Assembly reference cache cleared.");
    }
}