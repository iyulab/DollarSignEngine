namespace DollarSignEngine.Evaluation;

internal partial class ExpressionEvaluator
{
    /// <summary>
    /// Tries to handle special case expressions that DynamicExpresso might struggle with.
    /// </summary>
    private bool TryHandleSpecialCases(string expression, Dictionary<string, object?> parameters, out object? result, DollarSignOptions option)
    {
        result = null;

        // Case 1: Index from end operator (e.g., numbers[^1])
        var indexFromEndMatch = IndexFromEndRegex.Match(expression);
        if (indexFromEndMatch.Success)
        {
            string varName = indexFromEndMatch.Groups[1].Value;
            int indexFromEnd = int.Parse(indexFromEndMatch.Groups[2].Value);

            if (parameters.TryGetValue(varName, out var value))
            {
                if (value is Array arr)
                {
                    result = arr.GetValue(arr.Length - indexFromEnd);
                    return true;
                }
                else if (value is IList list)
                {
                    result = list[list.Count - indexFromEnd];
                    return true;
                }
                else if (value is IEnumerable<object> enumerable)
                {
                    result = enumerable.ElementAt(enumerable.Count() - indexFromEnd);
                    return true;
                }
            }
        }

        // Case 2: Simple string.Join pattern
        var stringJoinMatch = StringJoinFullRegex.Match(expression);
        if (stringJoinMatch.Success && !expression.Contains("Select") && !expression.Contains("Where"))
        {
            string separator = stringJoinMatch.Groups[1].Value;
            string collectionExpression = stringJoinMatch.Groups[2].Value;

            // Handle simple collection names
            if (IsSimpleVariableName(collectionExpression) && parameters.TryGetValue(collectionExpression, out var collection))
            {
                if (collection is IEnumerable enumerable)
                {
                    result = string.Join(separator, enumerable.Cast<object>());
                    return true;
                }
            }
        }

        // Case 3: LINQ transformations with complex expressions (Select, Where, OrderBy, etc.)
        var transformMatch = CollectionTransformRegex.Match(expression);
        if (transformMatch.Success)
        {
            string collectionName = transformMatch.Groups[1].Value;
            string method = transformMatch.Groups[2].Value;

            // Handle different LINQ methods
            switch (method)
            {
                case "Select":
                    if (HandleSelectExpression(expression, parameters, option, out result))
                        return true;
                    break;

                case "Where":
                    if (HandleWhereExpression(expression, parameters, option, out result))
                        return true;
                    break;

                case "OrderByDescending":
                    if (HandleOrderingExpression(expression, parameters, option, out result))
                        return true;
                    break;

                case "Count":
                    if (HandleCountExpression(expression, parameters, option, out result))
                        return true;
                    break;
            }
        }

        return false;
    }

    /// <summary>
    /// Handles Select expression patterns.
    /// </summary>
    private bool HandleSelectExpression(string expression, Dictionary<string, object?> parameters, DollarSignOptions option, out object? result)
    {
        result = null;

        // Handle pattern: collection.Select(x => x.ToUpper())
        var selectToUpperMatch = Regex.Match(expression, @"(\w+)\.Select\s*\(\s*(\w+)\s*=>\s*\2\.ToUpper\(\s*\)\s*\)");
        if (selectToUpperMatch.Success)
        {
            string collectionName = selectToUpperMatch.Groups[1].Value;

            if (parameters.TryGetValue(collectionName, out var collection) && collection is IEnumerable<string> strings)
            {
                result = strings.Select(s => s.ToUpper()).ToArray();
                return true;
            }
        }

        // Handle pattern: collection.Where(...).Select(x => x.Property)
        var whereSelectMatch = Regex.Match(expression, @"(\w+)\.Where\s*\(\s*(\w+)\s*=>\s*\2\.(\w+)\s*>=\s*(\d+)\s*\)\.Select\s*\(\s*(\w+)\s*=>\s*\5\.(\w+)\s*\)");
        if (whereSelectMatch.Success)
        {
            string collectionName = whereSelectMatch.Groups[1].Value;
            string filterPropName = whereSelectMatch.Groups[3].Value;
            int threshold = int.Parse(whereSelectMatch.Groups[4].Value);
            string selectPropName = whereSelectMatch.Groups[6].Value;

            if (parameters.TryGetValue(collectionName, out var collection) && collection is IEnumerable objects)
            {
                var results = new List<object?>();

                foreach (var item in objects)
                {
                    if (item == null) continue;

                    var filterProp = item.GetType().GetProperty(filterPropName);
                    var selectProp = item.GetType().GetProperty(selectPropName);

                    if (filterProp != null && selectProp != null)
                    {
                        var filterValue = filterProp.GetValue(item);
                        if (filterValue is int intValue && intValue >= threshold)
                        {
                            results.Add(selectProp.GetValue(item));
                        }
                    }
                }

                result = results;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Handles Where expression patterns.
    /// </summary>
    private bool HandleWhereExpression(string expression, Dictionary<string, object?> parameters, DollarSignOptions option, out object? result)
    {
        result = null;

        // Handle pattern: collection.Where(x => x.Property >= value)
        var whereMatch = Regex.Match(expression, @"(\w+)\.Where\s*\(\s*(\w+)\s*=>\s*\2\.(\w+)\s*>=\s*(\d+)\s*\)");
        if (whereMatch.Success)
        {
            string collectionName = whereMatch.Groups[1].Value;
            string propName = whereMatch.Groups[3].Value;
            int threshold = int.Parse(whereMatch.Groups[4].Value);

            if (parameters.TryGetValue(collectionName, out var collection) && collection is IEnumerable objects)
            {
                var results = new List<object?>();

                foreach (var item in objects)
                {
                    if (item == null) continue;

                    var prop = item.GetType().GetProperty(propName);
                    if (prop != null)
                    {
                        var value = prop.GetValue(item);
                        if (value is int intValue && intValue >= threshold)
                        {
                            results.Add(item);
                        }
                    }
                }

                result = results;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Handles ordering expression patterns.
    /// </summary>
    private bool HandleOrderingExpression(string expression, Dictionary<string, object?> parameters, DollarSignOptions option, out object? result)
    {
        result = null;

        // Handle pattern: collection.OrderByDescending(x => x).Take(count)
        var orderTakeMatch = Regex.Match(expression, @"(\w+)\.OrderByDescending\s*\(\s*\w+\s*=>\s*\w+\s*\)\.Take\s*\(\s*(\d+)\s*\)");
        if (orderTakeMatch.Success)
        {
            string collectionName = orderTakeMatch.Groups[1].Value;
            int count = int.Parse(orderTakeMatch.Groups[2].Value);

            if (parameters.TryGetValue(collectionName, out var collection))
            {
                if (collection is int[] intArray)
                {
                    result = intArray.OrderByDescending(x => x).Take(count).ToArray();
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Handles Count expression patterns.
    /// </summary>
    private bool HandleCountExpression(string expression, Dictionary<string, object?> parameters, DollarSignOptions option, out object? result)
    {
        result = null;

        // Handle pattern: collection.Count(x => x % 2 == 1)
        var countMatch = Regex.Match(expression, @"(\w+)\.Count\s*\(\s*\w+\s*=>\s*\w+\s*%\s*2\s*==\s*1\s*\)");
        if (countMatch.Success)
        {
            string collectionName = countMatch.Groups[1].Value;

            if (parameters.TryGetValue(collectionName, out var collection) && collection is int[] intArray)
            {
                result = intArray.Count(x => x % 2 == 1);
                return true;
            }
        }

        return false;
    }
}