using System.Linq.Expressions;

namespace DollarSignEngine.Internals;

/// <summary>
/// Provides high-performance property access through compiled expressions.
/// </summary>
internal class TypeAccessor
{
    private readonly Dictionary<string, Func<object, object?>> _getters;
    private readonly Type _type;

    /// <summary>
    /// Creates a new TypeAccessor for the specified type.
    /// </summary>
    public TypeAccessor(Type type)
    {
        _type = type ?? throw new ArgumentNullException(nameof(type));
        _getters = new Dictionary<string, Func<object, object?>>(StringComparer.OrdinalIgnoreCase);

        // Pre-cache all property getters for better performance
        BuildPropertyGetters();
    }

    /// <summary>
    /// Gets the type this accessor is for.
    /// </summary>
    public Type Type => _type;

    /// <summary>
    /// Builds compiled property getters for all public properties.
    /// </summary>
    private void BuildPropertyGetters()
    {
        var properties = _type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            if (property.CanRead && property.GetIndexParameters().Length == 0)
            {
                var getter = CreatePropertyGetter(property);
                _getters[property.Name] = getter;
            }
        }
    }

    /// <summary>
    /// Creates a compiled getter function for a property.
    /// </summary>
    private Func<object, object?> CreatePropertyGetter(PropertyInfo property)
    {
        // Parameter expression representing the target object
        var instanceParam = Expression.Parameter(typeof(object), "instance");

        // Convert instance to the correct type
        var instanceConvert = Expression.Convert(instanceParam, _type);

        // Property access expression
        var propertyAccess = Expression.Property(instanceConvert, property);

        // Convert property value to object type if needed
        var propertyValueCast = property.PropertyType.IsValueType
            ? Expression.Convert(propertyAccess, typeof(object))
            : (Expression)propertyAccess;

        // Create and compile the lambda expression
        var lambda = Expression.Lambda<Func<object, object?>>(
            propertyValueCast,
            instanceParam
        );

        return lambda.Compile();
    }

    /// <summary>
    /// Tries to get a property value from the specified instance.
    /// </summary>
    public bool TryGetPropertyValue(object instance, string propertyName, out object? value)
    {
        if (instance == null)
        {
            value = null;
            return false;
        }

        // Direct lookup
        if (_getters.TryGetValue(propertyName, out var getter))
        {
            try
            {
                value = getter(instance);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Debug($"[TypeAccessor.TryGetPropertyValue] Direct property access failed: {propertyName}, Error: {ex.Message}");
            }
        }

        // Case-insensitive lookup if not found directly
        foreach (var key in _getters.Keys)
        {
            if (string.Equals(key, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    value = _getters[key](instance);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Debug($"[TypeAccessor.TryGetPropertyValue] Case-insensitive property access failed: {key}, Error: {ex.Message}");
                }
                break;
            }
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Gets all property names that can be accessed.
    /// </summary>
    public IEnumerable<string> GetPropertyNames() => _getters.Keys;
}

/// <summary>
/// Factory and cache for TypeAccessor instances.
/// </summary>
internal static class TypeAccessorFactory
{
    // Cache with reasonable default size
    private static readonly LruCache<Type, TypeAccessor> _typeAccessors =
        new LruCache<Type, TypeAccessor>(500);

    /// <summary>
    /// Gets a TypeAccessor for the specified type, creating and caching it if necessary.
    /// </summary>
    public static TypeAccessor GetTypeAccessor(Type type)
    {
        return _typeAccessors.GetOrAdd(type, t => new TypeAccessor(t));
    }

    /// <summary>
    /// Clears the TypeAccessor cache.
    /// </summary>
    public static void ClearCache()
    {
        _typeAccessors.Clear();
        Logger.Debug("[TypeAccessorFactory.ClearCache] Type accessor cache cleared.");
    }
}