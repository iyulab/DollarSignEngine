using DynamicExpresso;

namespace DollarSignEngine.Evaluation;

/// <summary>
/// Evaluates expressions using DynamicExpresso with enhanced parameter handling.
/// </summary>
internal class ExpressionEvaluator
{
    private readonly ExpressionCache _cache;
    private readonly TernaryExpressionEvaluator _ternaryEvaluator;

    // Optimized regex patterns with Compiled option for better performance
    private static readonly Regex SimpleVariableRegex = new(@"^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);
    private static readonly Regex IndexFromEndRegex = new(@"(\w+)\s*\[\s*\^(\d+)\s*\]", RegexOptions.Compiled);

    /// <summary>
    /// Initializes a new instance of the ExpressionEvaluator class.
    /// </summary>
    public ExpressionEvaluator(ExpressionCache cache)
    {
        _cache = cache;
        _ternaryEvaluator = new TernaryExpressionEvaluator(this);
    }

    /// <summary>
    /// Evaluates an expression using the provided parameters.
    /// </summary>
    public object? Evaluate(string expression, object? parameter, DollarSignOptions options)
    {
        Log.Debug($"Evaluating expression: {expression}", options);

        // Handle empty expressions
        if (string.IsNullOrWhiteSpace(expression))
            return null;

        // Try custom variable resolver first
        if (TryResolveWithCustomResolver(expression, parameter, options, out var resolvedValue))
            return resolvedValue;

        // Transform index-from-end operator (^n)
        expression = TransformIndexFromEndOperator(expression);

        // Handle ternary expressions
        if (TernaryExpressionEvaluator.IsTernaryExpression(expression))
            return _ternaryEvaluator.EvaluateTernary(expression, parameter, options);

        // Handle direct variable access
        if (SimpleVariableRegex.IsMatch(expression) && parameter != null)
        {
            var parameters = ExtractParameters(parameter);
            if (parameters.TryGetValue(expression, out var value))
            {
                Log.Debug($"Direct variable access for {expression}: {value}", options);
                return value;
            }
        }

        return EvaluateWithCaching(expression, parameter, options);
    }

    /// <summary>
    /// Transforms index-from-end operator into standard indexing.
    /// </summary>
    private string TransformIndexFromEndOperator(string expression)
    {
        var indexFromEndMatch = IndexFromEndRegex.Match(expression);
        if (indexFromEndMatch.Success)
        {
            string arrayName = indexFromEndMatch.Groups[1].Value;
            int indexFromEnd = int.Parse(indexFromEndMatch.Groups[2].Value);

            // Replace with standard indexing: array[^1] -> array[array.Length - 1]
            return $"{arrayName}[{arrayName}.Length - {indexFromEnd}]";
        }

        return expression;
    }

    /// <summary>
    /// Attempts to resolve an expression using the custom variable resolver.
    /// </summary>
    private bool TryResolveWithCustomResolver(string expression, object? parameter, DollarSignOptions options, out object? result)
    {
        result = null;

        if (options.VariableResolver == null)
            return false;

        // Try with the complete expression
        result = options.VariableResolver(expression, parameter);
        if (result != null)
            return true;

        // Try with variable part if expression contains a dot
        if (!expression.Contains('.'))
            return false;

        var parts = ExtractVariableParts(expression);
        if (string.IsNullOrEmpty(parts.variableName))
            return false;

        var resolvedVariable = options.VariableResolver(parts.variableName, parameter);
        if (resolvedVariable == null)
            return false;

        // Create a temporary parameter dictionary with the resolved variable
        var tempParams = CreateCombinedParameters(parameter, parts.variableName, resolvedVariable);

        // Create an interpreter and try to evaluate
        try
        {
            var interpreter = CreateInterpreter(options).SetVariables(tempParams);
            result = interpreter.Eval(expression);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Creates a combined parameter dictionary with original parameters and a resolved variable.
    /// </summary>
    private Dictionary<string, object?> CreateCombinedParameters(object? parameter, string variableName, object? resolvedVariable)
    {
        var tempParams = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            { variableName, resolvedVariable }
        };

        // Add the original parameters too
        if (parameter != null)
        {
            var extractedParams = ExtractParameters(parameter);
            foreach (var pair in extractedParams)
            {
                if (!tempParams.ContainsKey(pair.Key))
                {
                    tempParams[pair.Key] = pair.Value;
                }
            }
        }

        return tempParams;
    }

    /// <summary>
    /// Evaluates an expression with caching support.
    /// </summary>
    private object? EvaluateWithCaching(string expression, object? parameter, DollarSignOptions options)
    {
        // Check cache before evaluating
        string cacheKey = string.Empty;
        if (options.EnableCaching)
        {
            cacheKey = $"{expression}_{parameter?.GetType().FullName ?? "null"}";
            if (_cache.TryGetLambda(cacheKey, out var cachedLambda))
            {
                try
                {
                    var result = cachedLambda(parameter);
                    Log.Debug($"Using cached lambda for expression: {expression}, result: {result}", options);
                    return result;
                }
                catch (Exception ex)
                {
                    Log.Debug($"Error executing cached lambda: {ex.Message}", options);
                }
            }
        }

        return EvaluateUsingInterpreter(expression, parameter, options, cacheKey);
    }

    /// <summary>
    /// Evaluates an expression using the DynamicExpresso interpreter.
    /// </summary>
    private object? EvaluateUsingInterpreter(string expression, object? parameter, DollarSignOptions options, string cacheKey)
    {
        try
        {
            // Extract parameters and create interpreter
            var parameters = ExtractParameters(parameter);
            var interpreter = CreateInterpreter(options).SetVariables(parameters);

            // Try evaluation strategies
            object? result = null;
            bool success = TryDirectEvaluation(interpreter, expression, ref result) ||
                           TryLambdaEvaluation(interpreter, expression, ref result);

            if (!success)
                throw new DollarSignEngineException($"Could not evaluate expression: {expression}");

            // Cache the successful result
            if (options.EnableCaching && !string.IsNullOrEmpty(cacheKey))
            {
                _cache.CacheLambda(cacheKey, _ => result);
            }

            return result;
        }
        catch (Exception ex)
        {
            Log.Debug($"Expression evaluation error: {ex.Message}", options);

            // Handle missing parameters based on options
            if (options.ThrowOnMissingParameter)
                throw new DollarSignEngineException($"Error evaluating expression '{expression}': {ex.Message}", ex);

            return null;
        }
    }

    /// <summary>
    /// Tries to evaluate an expression directly.
    /// </summary>
    private bool TryDirectEvaluation(Interpreter interpreter, string expression, ref object? result)
    {
        try
        {
            result = interpreter.Eval(expression);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Tries to evaluate an expression by wrapping it in a lambda.
    /// </summary>
    private bool TryLambdaEvaluation(Interpreter interpreter, string expression, ref object? result)
    {
        try
        {
            var lambda = interpreter.Parse<Func<object>>("() => " + expression);
            result = lambda();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Creates and configures an interpreter instance with necessary references.
    /// </summary>
    private Interpreter CreateInterpreter(DollarSignOptions options)
    {
        var interpreter = new Interpreter(InterpreterOptions.DefaultCaseInsensitive)
            .EnableReflection()
            .Reference(typeof(Enumerable))
            .Reference(typeof(Convert))
            .Reference(typeof(Math))
            .Reference(typeof(DateTime))
            .Reference(typeof(TimeSpan))
            .Reference(typeof(Guid))
            .Reference(typeof(String))
            .Reference(typeof(Boolean))
            .Reference(typeof(Char))
            .Reference(typeof(Object))
            .Reference(typeof(Int32))
            .Reference(typeof(Double))
            .Reference(typeof(Decimal))
            .Reference(typeof(List<>))
            .Reference(typeof(IEnumerable<>))
            .Reference(typeof(ICollection<>))
            .Reference(typeof(IList<>))
            .Reference(typeof(Dictionary<,>))
            .Reference(typeof(IDictionary<,>));

        // Add additional namespaces
        AddAdditionalNamespaces(interpreter, options);

        return interpreter;
    }

    /// <summary>
    /// Adds additional namespaces to the interpreter.
    /// </summary>
    private void AddAdditionalNamespaces(Interpreter interpreter, DollarSignOptions options)
    {
        foreach (var ns in options.AdditionalNamespaces)
        {
            try
            {
                var assembly = Assembly.Load(ns);
                if (assembly != null)
                {
                    var exportedTypes = assembly.GetExportedTypes()
                        .Where(t => !t.IsGenericTypeDefinition)
                        .Where(t => !t.IsAbstract || t.IsSealed)
                        .Take(100);

                    foreach (var type in exportedTypes)
                    {
                        try
                        {
                            interpreter.Reference(type);
                        }
                        catch
                        {
                            // Skip types that can't be referenced
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug($"Error processing namespace or assembly {ns}: {ex.Message}", options);
            }
        }
    }

    /// <summary>
    /// Extracts parameters from an object into a dictionary for use in expression evaluation.
    /// </summary>
    private Dictionary<string, object?> ExtractParameters(object? parameter)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (parameter == null)
            return result;

        // Add parameter with special names for direct access
        result["param"] = parameter;
        result["this"] = parameter;

        // Handle dictionary types
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

        // Extract properties from objects
        ExtractPropertiesAndMethods(parameter, result);

        return result;
    }

    /// <summary>
    /// Extracts properties and methods from an object.
    /// </summary>
    private void ExtractPropertiesAndMethods(object parameter, Dictionary<string, object?> result)
    {
        var type = parameter.GetType();

        // Extract properties
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            try
            {
                if (prop.CanRead)
                {
                    result[prop.Name] = prop.GetValue(parameter);
                }
            }
            catch
            {
                // Skip properties that can't be read
            }
        }

        // Extract methods
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => !m.IsSpecialName))
        {
            try
            {
                if (!result.ContainsKey(method.Name))
                {
                    result[method.Name] = CreateMethodDelegate(parameter, method);
                }
            }
            catch
            {
                // Skip methods that can't be handled
            }
        }
    }

    /// <summary>
    /// Creates a delegate for a method that can be invoked in expressions.
    /// </summary>
    private object CreateMethodDelegate(object target, MethodInfo method)
    {
        var parameters = method.GetParameters();

        // Handle parameterless methods
        if (parameters.Length == 0)
        {
            if (method.ReturnType == typeof(void))
            {
                // Action for void methods with no parameters
                return Delegate.CreateDelegate(typeof(Action), target, method);
            }
            else
            {
                // Func<TResult> for methods that return a value with no parameters
                var delegateType = typeof(Func<>).MakeGenericType(method.ReturnType);
                return Delegate.CreateDelegate(delegateType, target, method);
            }
        }

        // For methods with parameters, return the method as is
        return method;
    }

    /// <summary>
    /// Extracts the variable name and the rest of an expression containing a method call.
    /// </summary>
    private (string variableName, string rest) ExtractVariableParts(string expression)
    {
        // Handle simple cases first
        int dotIndex = expression.IndexOf('.');
        if (dotIndex <= 0)
        {
            return (string.Empty, expression);
        }

        // For more complex cases, we need to handle proper parsing
        bool inString = false;
        char stringDelimiter = '\0';
        int parenDepth = 0;
        int bracketDepth = 0;

        for (int i = 0; i < expression.Length; i++)
        {
            char c = expression[i];

            // Handle string literals
            if ((c == '"' || c == '\'') && (i == 0 || expression[i - 1] != '\\'))
            {
                if (!inString)
                {
                    inString = true;
                    stringDelimiter = c;
                }
                else if (c == stringDelimiter)
                {
                    inString = false;
                }
                continue;
            }

            if (inString) continue;

            // Track nesting
            if (c == '(') parenDepth++;
            else if (c == ')') parenDepth--;
            else if (c == '[') bracketDepth++;
            else if (c == ']') bracketDepth--;

            // Find the first dot outside of any nesting
            else if (c == '.' && parenDepth == 0 && bracketDepth == 0)
            {
                string variablePart = expression.Substring(0, i).Trim();

                // Validate the variable part
                if (SimpleVariableRegex.IsMatch(variablePart))
                {
                    return (variablePart, expression);
                }
                return (string.Empty, expression);
            }
        }

        return (string.Empty, expression);
    }
}