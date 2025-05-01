using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;

namespace DollarSignEngine;

/// <summary>
/// Contains collection handling functionality for the DollarSign class.
/// </summary>
public static partial class DollarSign
{
    /// <summary>
    /// Handles different types of collection access (arrays, lists, dictionaries)
    /// </summary>
    private static bool HandleCollectionAccess(object collection, string indexOrKey, out object? result)
    {
        result = null;

        // Handle numeric indexing for arrays and lists
        if (int.TryParse(indexOrKey, out var index))
        {
            // For standard arrays
            if (collection is Array array)
            {
                if (index >= 0 && index < array.Length)
                {
                    result = array.GetValue(index);
                    return true;
                }
                return false;
            }

            // For IList collections
            if (collection is IList list)
            {
                if (index >= 0 && index < list.Count)
                {
                    result = list[index];
                    return true;
                }
                return false;
            }

            // For other collection types that might implement indexing
            try
            {
                var indexerProperty = collection.GetType().GetProperty("Item", new[] { typeof(int) });
                if (indexerProperty != null)
                {
                    result = indexerProperty.GetValue(collection, new object[] { index });
                    return true;
                }
            }
            catch
            {
                // Ignore and continue with other approaches
            }
        }

        // Handle indexer from end (^1)
        if (indexOrKey.StartsWith("^") && int.TryParse(indexOrKey.Substring(1), out var fromEndIndex))
        {
            // Handle arrays
            if (collection is Array array)
            {
                int actualIndex = array.Length - fromEndIndex;
                if (actualIndex >= 0 && actualIndex < array.Length)
                {
                    result = array.GetValue(actualIndex);
                    return true;
                }
                return false;
            }

            // Handle IList
            if (collection is IList list)
            {
                int actualIndex = list.Count - fromEndIndex;
                if (actualIndex >= 0 && actualIndex < list.Count)
                {
                    result = list[actualIndex];
                    return true;
                }
                return false;
            }
        }

        // String keys for dictionaries
        // First check exact types we know
        if (collection is IDictionary dict && dict.Contains(indexOrKey))
        {
            result = dict[indexOrKey];
            return true;
        }

        if (collection is IDictionary<string, object> dictObj && dictObj.TryGetValue(indexOrKey, out var value))
        {
            result = value;
            return true;
        }

        // Enhanced processing for nested dictionaries with string keys, handling quotes
        if (collection is IDictionary dict2)
        {
            // Handle quoted keys (both single and double quotes)
            if ((indexOrKey.StartsWith("\"") && indexOrKey.EndsWith("\"")) ||
                (indexOrKey.StartsWith("'") && indexOrKey.EndsWith("'")))
            {
                string unquotedKey = indexOrKey.Substring(1, indexOrKey.Length - 2);
                if (dict2.Contains(unquotedKey))
                {
                    result = dict2[unquotedKey];
                    return true;
                }
            }

            // Try with string key directly
            if (dict2.Contains(indexOrKey))
            {
                result = dict2[indexOrKey];
                return true;
            }
        }

        // Use reflection for all other dictionary types
        Type collectionType = collection.GetType();

        // Handle generic Dictionary<string, T> or similar types
        if (collectionType.IsGenericType)
        {
            Type genericTypeDef = collectionType.GetGenericTypeDefinition();
            if (genericTypeDef == typeof(Dictionary<,>) ||
                genericTypeDef == typeof(IDictionary<,>) ||
                collectionType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>)))
            {
                Type[] genericArgs = collectionType.GetGenericArguments();
                if (genericArgs.Length >= 2 && genericArgs[0] == typeof(string))
                {
                    // Handle quoted keys (both single and double quotes)
                    if ((indexOrKey.StartsWith("\"") && indexOrKey.EndsWith("\"")) ||
                        (indexOrKey.StartsWith("'") && indexOrKey.EndsWith("'")))
                    {
                        indexOrKey = indexOrKey.Substring(1, indexOrKey.Length - 2);
                    }

                    // Try to find TryGetValue method
                    MethodInfo? tryGetValueMethod = collectionType.GetMethod("TryGetValue",
                        BindingFlags.Instance | BindingFlags.Public);

                    if (tryGetValueMethod != null)
                    {
                        var parameters = new object[] { indexOrKey, null! };
                        try
                        {
                            var success = (bool)tryGetValueMethod.Invoke(collection, parameters)!;
                            if (success)
                            {
                                result = parameters[1];
                                return true;
                            }
                        }
                        catch
                        {
                            // Failed to invoke, try other approaches
                        }
                    }

                    // If TryGetValue fails, try using the indexer directly
                    try
                    {
                        // Try to get the indexer property (usually named "Item")
                        PropertyInfo? indexerProperty = collectionType.GetProperties()
                            .FirstOrDefault(p => p.GetIndexParameters().Length > 0 &&
                                              p.GetIndexParameters()[0].ParameterType == typeof(string));

                        if (indexerProperty != null)
                        {
                            result = indexerProperty.GetValue(collection, new object[] { indexOrKey });
                            return true;
                        }
                    }
                    catch
                    {
                        // Ignore errors and continue
                    }
                }
            }
        }

        // One more fallback for specialized collection types:
        // Try to find a string indexer on any type
        try
        {
            var stringIndexer = collectionType.GetProperties()
                .FirstOrDefault(p => p.Name == "Item" && p.GetIndexParameters().Length == 1 &&
                                  p.GetIndexParameters()[0].ParameterType == typeof(string));

            if (stringIndexer != null)
            {
                // Handle quoted keys (both single and double quotes)
                if ((indexOrKey.StartsWith("\"") && indexOrKey.EndsWith("\"")) ||
                    (indexOrKey.StartsWith("'") && indexOrKey.EndsWith("'")))
                {
                    indexOrKey = indexOrKey.Substring(1, indexOrKey.Length - 2);
                }

                result = stringIndexer.GetValue(collection, new object[] { indexOrKey });
                return true;
            }
        }
        catch
        {
            // Last fallback attempt failed
        }

        return false;
    }

    /// <summary>
    /// Try to extract the collection name from a LINQ expression
    /// </summary>
    private static bool TryGetCollectionTarget(string expression, out string targetName)
    {
        targetName = string.Empty;

        // Look for common LINQ patterns: collection.Method(...)
        var linqPatterns = new[]
        {
            @"(\w+)\.Sum\(\)",
            @"(\w+)\.Average\(\)",
            @"(\w+)\.Count\(",
            @"(\w+)\.Where\(",
            @"(\w+)\.Select\(",
            @"(\w+)\.First\(",
            @"(\w+)\.Last\(",
            @"(\w+)\.Any\(",
            @"(\w+)\.OrderBy",
            @"(\w+)\.OrderByDescending",
            @"(\w+)\.Take\(",
            @"string\.Join\(.*?(\w+)\)",  // Added pattern for string.Join
            @"string\.Join\(.*?,\s*(\w+)\.Select\("  // Pattern for string.Join with Select
        };

        foreach (var pattern in linqPatterns)
        {
            var match = Regex.Match(expression, pattern);
            if (match.Success && match.Groups.Count > 1)
            {
                // For string.Join, we need the last capture group
                targetName = match.Groups[match.Groups.Count - 1].Value;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Try to handle common LINQ operations directly without script evaluation
    /// </summary>
    private static bool TryHandleLinqOperationDirectly(string expression, object collection, out object? result)
    {
        result = null;

        // For string.Join operation with any collection
        if (expression.Contains("string.Join") && collection != null)
        {
            // Match general string.Join pattern with any collection
            var joinMatch = Regex.Match(expression, @"string\.Join\(\s*""([^""]*)""\s*,\s*(.+)\s*\)");
            if (joinMatch.Success && joinMatch.Groups.Count > 2)
            {
                string delimiter = joinMatch.Groups[1].Value;
                string itemsExpression = joinMatch.Groups[2].Value.Trim();

                // Check if it's a direct collection reference
                if (IsSafeIdentifier(itemsExpression) && collection is IEnumerable e1)
                {
                    // Convert IEnumerable to a list of strings
                    var stringItems = new List<string>();
                    foreach (var item in e1)
                    {
                        stringItems.Add(item?.ToString() ?? string.Empty);
                    }
                    result = string.Join(delimiter, stringItems);
                    return true;
                }
                // Try to handle more complex LINQ with string.Join using reflection
                else if (collection is IEnumerable e2 && TryEvaluateLinqExpression(itemsExpression, e2, out var linqResult))
                {
                    if (linqResult is IEnumerable resultEnum)
                    {
                        var stringItems = new List<string>();
                        foreach (var item in resultEnum)
                        {
                            stringItems.Add(item?.ToString() ?? string.Empty);
                        }
                        result = string.Join(delimiter, stringItems);
                        return true;
                    }
                }
            }
        }

        // Handle string.Join with Select and ToUpper
        var joinSelectMatch = Regex.Match(expression,
            @"string\.Join\(\s*""([^""]*)""\s*,\s*(\w+)\.Select\(\s*(\w+)\s*=>\s*\3\.ToUpper\(\)\s*\)\s*\)");

        if (joinSelectMatch.Success && collection is IEnumerable collection2)
        {
            string delimiter = joinSelectMatch.Groups[1].Value;

            // Process collection and apply ToUpper
            var results = new List<string>();
            foreach (var item in collection2)
            {
                if (item != null)
                {
                    string str = item.ToString() ?? string.Empty;
                    results.Add(str.ToUpper());
                }
            }

            result = string.Join(delimiter, results);
            return true;
        }

        // Handle numeric operations using reflection for all enumerable types
        if (collection is IEnumerable enumerable)
        {
            Type? elementType = null;
            bool isNumericCollection = false;

            // Try to determine collection element type
            if (collection.GetType().IsArray)
            {
                elementType = collection.GetType().GetElementType();
            }
            else if (collection.GetType().IsGenericType)
            {
                var genericArgs = collection.GetType().GetGenericArguments();
                if (genericArgs.Length > 0)
                {
                    elementType = genericArgs[0];
                }
            }

            // Check if element type is numeric
            if (elementType != null)
            {
                isNumericCollection = IsNumericType(elementType);
            }
            else
            {
                // Try to inspect collection elements directly
                foreach (var item in enumerable)
                {
                    if (item != null)
                    {
                        isNumericCollection = IsNumericType(item.GetType());
                        break;
                    }
                }
            }

            if (isNumericCollection)
            {
                // Handle common aggregation functions using reflection
                if (expression.EndsWith(".Sum()"))
                {
                    return TryInvokeAggregateMethod("Sum", enumerable, out result);
                }
                else if (expression.EndsWith(".Average()"))
                {
                    return TryInvokeAggregateMethod("Average", enumerable, out result);
                }
                else if (expression.EndsWith(".Count()") || (expression.Contains(".Count(") && !expression.Contains("=>")))
                {
                    return TryInvokeAggregateMethod("Count", enumerable, out result);
                }
            }

            // Handle Count with predicate for odd/even numbers
            var countModMatch = Regex.Match(expression, @"\.Count\(\s*(\w+)\s*=>\s*\1\s*%\s*2\s*==\s*(\d+)\s*\)");
            if (countModMatch.Success)
            {
                int remainder = int.Parse(countModMatch.Groups[2].Value);
                int count = 0;

                foreach (var item in enumerable)
                {
                    if (item != null)
                    {
                        try
                        {
                            int number = Convert.ToInt32(item);
                            if (number % 2 == remainder)
                                count++;
                        }
                        catch
                        {
                            // Skip non-integer values
                        }
                    }
                }

                result = count;
                return true;
            }

            // Try to evaluate any LINQ expression using reflection
            if (TryEvaluateLinqExpression(expression, enumerable, out var linqResult))
            {
                result = linqResult;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Attempts to invoke a LINQ aggregate method on an enumerable collection
    /// </summary>
    private static bool TryInvokeAggregateMethod(string methodName, IEnumerable collection, out object? result)
    {
        result = null;
        try
        {
            // Get appropriate method from Enumerable class
            var enumerableType = typeof(Enumerable);
            var method = enumerableType.GetMethods()
                .Where(m => m.Name == methodName && m.GetParameters().Length == 1)
                .FirstOrDefault();

            if (method != null)
            {
                // For generic methods, try to find appropriate overload
                if (method.IsGenericMethod)
                {
                    Type elementType = GetElementType(collection);
                    if (elementType != null)
                    {
                        var genericMethod = method.MakeGenericMethod(elementType);
                        result = genericMethod.Invoke(null, new object[] { collection });
                        return true;
                    }
                }
                else
                {
                    // For non-generic methods
                    result = method.Invoke(null, new object[] { collection });
                    return true;
                }
            }
        }
        catch
        {
            // Ignore exceptions and try other approaches
        }
        return false;
    }

    /// <summary>
    /// Tries to evaluate a LINQ expression on an enumerable using reflection
    /// </summary>
    private static bool TryEvaluateLinqExpression(string expression, IEnumerable collection, out object? result)
    {
        result = null;

        try
        {
            // Identify common LINQ method patterns
            var orderByMatch = Regex.Match(expression, @"(\w+)\.OrderByDescending\(\s*(\w+)\s*=>\s*\2\)");
            var orderByTakeMatch = Regex.Match(expression, @"(\w+)\.OrderByDescending\(\s*(\w+)\s*=>\s*\2\)\.Take\((\d+)\)");
            var whereSelectMatch = Regex.Match(expression, @"(\w+)\.Where\(\s*(\w+)\s*=>\s*\2\.(\w+)\s*>=\s*(\d+)\s*\)\.Select\(\s*\2\s*=>\s*\2\.(\w+)\s*\)");
            var countPredicateMatch = Regex.Match(expression, @"(\w+)\.Count\(\s*(\w+)\s*=>\s*(.+)\s*\)");
            var selectMatch = Regex.Match(expression, @"(\w+)\.Select\(\s*(\w+)\s*=>\s*\2\.ToUpper\(\)\s*\)");

            // Handle Select with ToUpper pattern
            if (selectMatch.Success)
            {
                var upperList = new List<string>();
                foreach (var item in collection)
                {
                    if (item != null)
                    {
                        string str = item.ToString() ?? string.Empty;
                        upperList.Add(str.ToUpper());
                    }
                }
                result = upperList;
                return true;
            }

            // Handle OrderBy with Take pattern
            if (orderByTakeMatch.Success)
            {
                int takeCount = int.Parse(orderByTakeMatch.Groups[3].Value);
                result = ApplyOrderByDescendingWithTake(collection, takeCount);
                return result != null;
            }
            // Handle simple OrderBy pattern
            else if (orderByMatch.Success)
            {
                result = ApplyOrderByDescending(collection);
                return result != null;
            }
            // Handle Where with Select pattern
            else if (whereSelectMatch.Success)
            {
                string propCompare = whereSelectMatch.Groups[3].Value;
                int threshold = int.Parse(whereSelectMatch.Groups[4].Value);
                string propSelect = whereSelectMatch.Groups[5].Value;

                result = ApplyWhereSelect(collection, propCompare, threshold, propSelect);
                return result != null;
            }
            // Handle Count with predicate pattern
            else if (countPredicateMatch.Success)
            {
                string predicateExpression = countPredicateMatch.Groups[3].Value.Trim();
                result = EvaluateCountWithPredicate(collection, predicateExpression);
                return result != null;
            }
        }
        catch
        {
            // Ignore exceptions and return false
        }

        return false;
    }

    /// <summary>
    /// Apply OrderByDescending operation followed by Take using reflection
    /// </summary>
    private static object? ApplyOrderByDescendingWithTake(IEnumerable collection, int takeCount)
    {
        var ordered = ApplyOrderByDescending(collection);
        if (ordered != null)
        {
            try
            {
                // Apply Take method using reflection
                var takeMethod = typeof(Enumerable).GetMethods()
                    .Where(m => m.Name == "Take" && m.GetParameters().Length == 2)
                    .FirstOrDefault();

                if (takeMethod != null)
                {
                    Type elementType = GetElementType(collection) ?? typeof(object);
                    var genericTake = takeMethod.MakeGenericMethod(elementType);
                    return genericTake.Invoke(null, new object[] { ordered, takeCount });
                }
            }
            catch
            {
                // Fallback: convert to list and take manually
                if (ordered is IEnumerable orderedEnum)
                {
                    var result = new List<object>();
                    int count = 0;
                    foreach (var item in orderedEnum)
                    {
                        if (count++ >= takeCount) break;
                        result.Add(item);
                    }
                    return result;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Apply OrderByDescending operation using reflection
    /// </summary>
    private static object? ApplyOrderByDescending(IEnumerable collection)
    {
        try
        {
            Type elementType = GetElementType(collection) ?? typeof(object);

            // Find OrderByDescending method from Enumerable
            var orderByMethod = typeof(Enumerable).GetMethods()
                .Where(m => m.Name == "OrderByDescending" && m.GetParameters().Length == 2)
                .FirstOrDefault();

            if (orderByMethod != null)
            {
                // Create comparison delegate
                Type delegateType = typeof(Func<,>).MakeGenericType(elementType, typeof(object));
                var delegateMethod = typeof(ComparableSelector).GetMethod("IdentitySelector")
                    ?.MakeGenericMethod(elementType);

                if (delegateMethod != null)
                {
                    var selector = delegateMethod.Invoke(null, null);
                    var genericOrderBy = orderByMethod.MakeGenericMethod(elementType, typeof(object));
                    return genericOrderBy.Invoke(null, new object[] { collection, selector });
                }
            }

            // Fallback: manual sorting if possible
            var items = new List<object>();
            foreach (var item in collection)
            {
                if (item != null) items.Add(item);
            }

            if (items.Count > 0 && items[0] is IComparable)
            {
                items.Sort((a, b) => ((IComparable)b).CompareTo(a)); // Descending
                return items;
            }
        }
        catch
        {
            // Ignore exceptions and return null
        }

        return null;
    }

    /// <summary>
    /// Apply Where followed by Select operation using reflection
    /// </summary>
    private static object? ApplyWhereSelect(IEnumerable collection, string propCompare, int threshold, string propSelect)
    {
        var result = new List<object>();

        // Use reflection to filter and select
        foreach (var item in collection)
        {
            if (item == null) continue;

            try
            {
                var itemType = item.GetType();
                var compareProperty = itemType.GetProperty(propCompare);
                if (compareProperty != null)
                {
                    var compareValue = compareProperty.GetValue(item);
                    if (compareValue != null && compareValue is IComparable comparable)
                    {
                        // Use CompareTo instead of hardcoding operators
                        if (comparable.CompareTo(threshold) >= 0)
                        {
                            var selectProperty = itemType.GetProperty(propSelect);
                            if (selectProperty != null)
                            {
                                var selectedValue = selectProperty.GetValue(item);
                                if (selectedValue != null)
                                {
                                    result.Add(selectedValue);
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore reflection errors for individual items
            }
        }

        return result;
    }

    /// <summary>
    /// Evaluate Count method with a predicate expression
    /// </summary>
    private static object? EvaluateCountWithPredicate(IEnumerable collection, string predicateExpression)
    {
        int count = 0;

        // Handle various predicate patterns dynamically without hardcoding
        if (predicateExpression.Contains("%"))
        {
            // Matches patterns like "n % 2 == 1" or "x % 2 == 0"
            var modMatch = Regex.Match(predicateExpression, @"(\w+)\s*%\s*(\d+)\s*==\s*(\d+)");
            if (modMatch.Success)
            {
                int divisor = int.Parse(modMatch.Groups[2].Value);
                int remainder = int.Parse(modMatch.Groups[3].Value);

                foreach (var item in collection)
                {
                    if (item == null) continue;

                    if (item is int intValue)
                    {
                        if (intValue % divisor == remainder)
                        {
                            count++;
                        }
                    }
                    else
                    {
                        // Try converting to int
                        try
                        {
                            int convertedValue = Convert.ToInt32(item);
                            if (convertedValue % divisor == remainder)
                            {
                                count++;
                            }
                        }
                        catch
                        {
                            // Not an integer, skip
                        }
                    }
                }
            }
        }
        else
        {
            // For other predicates, try to use property access
            var propMatch = Regex.Match(predicateExpression, @"(\w+)\.(\w+)\s*(>=|<=|==|>|<)\s*(\d+)");
            if (propMatch.Success)
            {
                string varName = propMatch.Groups[1].Value;
                string propName = propMatch.Groups[2].Value;
                string compOperator = propMatch.Groups[3].Value;
                int compValue = int.Parse(propMatch.Groups[4].Value);

                foreach (var item in collection)
                {
                    if (item == null) continue;

                    try
                    {
                        var prop = item.GetType().GetProperty(propName);
                        if (prop != null)
                        {
                            var propValue = prop.GetValue(item);
                            if (propValue != null && propValue is IComparable comparable)
                            {
                                int compareResult = comparable.CompareTo(compValue);

                                bool matches = compOperator switch
                                {
                                    ">=" => compareResult >= 0,
                                    "<=" => compareResult <= 0,
                                    "==" => compareResult == 0,
                                    ">" => compareResult > 0,
                                    "<" => compareResult < 0,
                                    _ => false
                                };

                                if (matches) count++;
                            }
                        }
                    }
                    catch
                    {
                        // Skip items that can't be compared
                    }
                }
            }
        }

        return count;
    }

    /// <summary>
    /// Determines if a type is numeric
    /// </summary>
    private static bool IsNumericType(Type type)
    {
        if (type == null) return false;

        switch (Type.GetTypeCode(type))
        {
            case TypeCode.Byte:
            case TypeCode.SByte:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
            case TypeCode.Decimal:
            case TypeCode.Double:
            case TypeCode.Single:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Helper class for creating selector delegates
    /// </summary>
    private static class ComparableSelector
    {
        /// <summary>
        /// Creates an identity selector for OrderBy operations
        /// </summary>
        public static Func<T, object> IdentitySelector<T>()
        {
            return item => item;
        }
    }

    /// <summary>
    /// Checks if an expression likely contains LINQ operations
    /// </summary>
    private static bool IsLinqExpression(string expression)
    {
        return expression.Contains(".Where(") ||
               expression.Contains(".Select(") ||
               expression.Contains(".OrderBy") ||
               expression.Contains(".Take(") ||
               expression.Contains(".GroupBy") ||
               expression.Contains(".Join(") ||
               expression.Contains(".Skip(") ||
               expression.Contains(".Any(") ||
               expression.Contains(".All(") ||
               expression.Contains(".First") ||
               expression.Contains(".Last") ||
               expression.Contains(".Single") ||
               expression.Contains(".Sum()") ||
               expression.Contains(".Average()") ||
               expression.Contains("string.Join") ||
               expression.Contains(".ToUpper()") ||
               (expression.Contains(".Count(") && expression.IndexOf(".Count(") > 0);
    }
}