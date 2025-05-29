namespace DollarSignEngine.Internals;

/// <summary>
/// Helper for type name operations with built-in caching and enhanced LINQ support.
/// </summary>
internal static class TypeNameHelper
{
    private static readonly LruCache<Type, string> _typeNameCache =
        new LruCache<Type, string>(1000);

    private static readonly LruCache<Type, bool> _anonymousTypeCache =
        new LruCache<Type, bool>(500);

    private static readonly LruCache<Type, bool> _collectionTypeCache =
        new LruCache<Type, bool>(500);

    private static readonly LruCache<Type, bool> _dictionaryTypeCache =
        new LruCache<Type, bool>(500);

    private static readonly LruCache<Type, bool> _linqIteratorTypeCache =
        new LruCache<Type, bool>(500);

    /// <summary>
    /// Determines if a type is an anonymous type.
    /// </summary>
    public static bool IsAnonymousType(Type? type)
    {
        if (type == null) return false;

        return _anonymousTypeCache.GetOrAdd(type, t =>
            Attribute.IsDefined(t, typeof(CompilerGeneratedAttribute), false)
            && t.IsGenericType && t.Name.Contains("AnonymousType")
            && (t.Name.StartsWith("<>") || t.Name.StartsWith("VB$"))
            && (t.Attributes & TypeAttributes.NotPublic) == TypeAttributes.NotPublic
        );
    }

    /// <summary>
    /// Determines if a type is a LINQ iterator or deferred execution type.
    /// </summary>
    public static bool IsLinqIteratorType(Type? type)
    {
        if (type == null) return false;

        return _linqIteratorTypeCache.GetOrAdd(type, t =>
        {
            var typeName = t.Name;
            var fullName = t.FullName ?? "";

            return typeName.Contains("Iterator") ||
                   typeName.Contains("Enumerable") ||
                   fullName.Contains("System.Linq") ||
                   (t.IsGenericType && t.GetGenericTypeDefinition().FullName?.Contains("System.Linq") == true) ||
                   t.Assembly.GetName().Name == "System.Linq" ||
                   (t.IsGenericType && t.GetGenericTypeDefinition().Assembly.GetName().Name == "System.Linq");
        });
    }

    /// <summary>
    /// Determines if a type is a collection (but not a dictionary).
    /// </summary>
    public static bool IsCollectionType(Type type)
    {
        return _collectionTypeCache.GetOrAdd(type, t =>
        {
            if (t.IsArray) return true;

            if (t.IsGenericType)
            {
                var genericTypeDef = t.GetGenericTypeDefinition();
                return genericTypeDef == typeof(List<>) ||
                       genericTypeDef == typeof(IEnumerable<>) ||
                       genericTypeDef == typeof(ICollection<>) ||
                       genericTypeDef == typeof(IList<>) ||
                       typeof(IEnumerable).IsAssignableFrom(t);
            }

            return typeof(IEnumerable).IsAssignableFrom(t) && !IsDictionaryType(t);
        });
    }

    /// <summary>
    /// Determines if a type is a dictionary type.
    /// </summary>
    public static bool IsDictionaryType(Type type)
    {
        return _dictionaryTypeCache.GetOrAdd(type, t =>
        {
            if (t.IsGenericType)
            {
                Type genericDef = t.GetGenericTypeDefinition();
                return genericDef == typeof(Dictionary<,>) ||
                       genericDef == typeof(IDictionary<,>) ||
                       genericDef == typeof(IReadOnlyDictionary<,>);
            }

            return typeof(IDictionary).IsAssignableFrom(t);
        });
    }

    /// <summary>
    /// Gets a C# parsable type name for a given type, with caching for performance.
    /// </summary>
    public static string GetParsableTypeName(Type type)
    {
        return _typeNameCache.GetOrAdd(type, t => CalculateParsableTypeName(t));
    }

    /// <summary>
    /// Internal method to calculate the parsable type name with enhanced LINQ handling.
    /// </summary>
    private static string CalculateParsableTypeName(Type type)
    {
        if (IsAnonymousType(type)) return "dynamic";

        // Handle LINQ iterator types
        if (IsLinqIteratorType(type)) return "dynamic";

        var underlyingType = Nullable.GetUnderlyingType(type);
        if (underlyingType != null)
        {
            return GetParsableTypeName(underlyingType) + "?";
        }

        if (type.IsArray)
        {
            var elementType = type.GetElementType();
            if (elementType != null)
            {
                int rank = type.GetArrayRank();
                string commas = rank > 1 ? new string(',', rank - 1) : "";

                string elementTypeName = GetParsableTypeName(elementType);

                if (IsAnonymousType(elementType) || elementType == typeof(object))
                {
                    return "dynamic[]";
                }

                return $"{elementTypeName}[{commas}]";
            }
        }

        if (IsDictionaryType(type))
        {
            return "dynamic";
        }

        if (type.IsGenericType)
        {
            var genericTypeDef = type.GetGenericTypeDefinition();
            var genericArgs = type.GetGenericArguments();

            if ((genericTypeDef == typeof(List<>) ||
                genericTypeDef == typeof(IEnumerable<>) ||
                genericTypeDef == typeof(ICollection<>) ||
                genericTypeDef == typeof(IList<>)) &&
                genericArgs.Length == 1)
            {
                var elementType = genericArgs[0];

                if (IsAnonymousType(elementType) || elementType == typeof(object))
                {
                    return "dynamic";
                }

                string baseName = genericTypeDef.FullName?.Split('`')[0] ?? genericTypeDef.Name.Split('`')[0];
                baseName = baseName.Replace('+', '.');
                var elementTypeName = GetParsableTypeName(elementType);
                return $"{baseName}<{elementTypeName}>";
            }
        }

        // Handle primitive types
        if (type == typeof(bool)) return "bool";
        if (type == typeof(byte)) return "byte";
        if (type == typeof(sbyte)) return "sbyte";
        if (type == typeof(char)) return "char";
        if (type == typeof(decimal)) return "decimal";
        if (type == typeof(double)) return "double";
        if (type == typeof(float)) return "float";
        if (type == typeof(int)) return "int";
        if (type == typeof(uint)) return "uint";
        if (type == typeof(long)) return "long";
        if (type == typeof(ulong)) return "ulong";
        if (type == typeof(short)) return "short";
        if (type == typeof(ushort)) return "ushort";
        if (type == typeof(string)) return "string";
        if (type == typeof(object)) return "dynamic";
        if (type == typeof(void)) return "void";

        // Handle ExpandoObject and Dynamic types
        if (type == typeof(System.Dynamic.ExpandoObject) ||
            type == typeof(System.Dynamic.DynamicObject) ||
            typeof(System.Dynamic.IDynamicMetaObjectProvider).IsAssignableFrom(type))
        {
            return "dynamic";
        }

        // Handle other generic types
        if (type.IsGenericType)
        {
            var genericTypeDef = type.GetGenericTypeDefinition();
            var genericArgs = type.GetGenericArguments().Select(GetParsableTypeName);

            string baseName = genericTypeDef.FullName?.Split('`')[0] ?? genericTypeDef.Name.Split('`')[0];
            baseName = baseName.Replace('+', '.');
            return $"{baseName}<{string.Join(", ", genericArgs)}>";
        }

        // Default case
        var fullName = type.FullName ?? type.Name;
        return fullName.Replace('+', '.');
    }

    /// <summary>
    /// Clears all type-related caches.
    /// </summary>
    public static void ClearCaches()
    {
        _typeNameCache.Clear();
        _anonymousTypeCache.Clear();
        _collectionTypeCache.Clear();
        _dictionaryTypeCache.Clear();
        _linqIteratorTypeCache.Clear();
        Logger.Debug("[TypeNameHelper.ClearCaches] All type name caches cleared.");
    }
}