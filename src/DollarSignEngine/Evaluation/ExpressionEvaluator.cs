namespace DollarSignEngine.Evaluation;

/// <summary>
/// Evaluates expressions using DynamicExpresso with enhanced capabilities.
/// </summary>
internal partial class ExpressionEvaluator
{
    private readonly ExpressionCache _cache;
    private readonly TernaryExpressionEvaluator _ternaryEvaluator = new();

    // Regular expression to match a simple variable name
    private static readonly Regex SimpleVariableRegex = new(@"^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);
    // Regular expression to find all variable names in an expression
    private static readonly Regex VariablesRegex = new(@"\b[a-zA-Z_][a-zA-Z0-9_]*\b", RegexOptions.Compiled);
    // Regular expression to identify index from end operator
    private static readonly Regex IndexFromEndRegex = new(@"(\w+)\s*\[\s*\^(\d+)\s*\]", RegexOptions.Compiled);
    // Regular expression for string.Join pattern with full collection chain
    private static readonly Regex StringJoinFullRegex = new(@"string\.Join\s*\(\s*""([^""]+)""\s*,\s*(.+?)\s*\)", RegexOptions.Compiled);
    // Regular expression for collection transformations (Select/Where/Order)
    private static readonly Regex CollectionTransformRegex = new(@"(\w+)\.(Select|Where|OrderByDescending|OrderBy|Take|Count)\s*\(", RegexOptions.Compiled);
    // Regular expression for finding lambda parameters
    private static readonly Regex LambdaParamRegex = new(@"(\w+)\s*=>\s*", RegexOptions.Compiled);
    // Regular expression for method invocation
    private static readonly Regex MethodInvocationRegex = new(@"^(\w+)\(\)$", RegexOptions.Compiled);
    // Regular expression for method chains like "variable.Method()"
    private static readonly Regex MethodChainRegex = new(@"^(\w+)\.(\w+)\(\)$", RegexOptions.Compiled);

    /// <summary>
    /// Initializes a new instance of the ExpressionEvaluator class.
    /// </summary>
    public ExpressionEvaluator(ExpressionCache cache)
    {
        _cache = cache;
    }

    /// <summary>
    /// Evaluates an expression using the provided parameters.
    /// </summary>
    public object? Evaluate(string expression, object? parameter, DollarSignOptions option)
    {
        Log.Debug($"Evaluating expression: {expression}", option);

        // Parse parameters from the parameter object
        var parameters = ExtractParameters(parameter);

        // Add the parameter itself as 'this' for method calls
        if (parameter != null)
        {
            parameters["this"] = parameter;
        }

        // Check for ternary operator pattern - now using direct token scanning instead of regex
        if (ContainsTernaryOperator(expression))
        {
            try
            {
                var result = _ternaryEvaluator.Evaluate(expression, parameters, option);
                Log.Debug($"Ternary operator evaluated successfully: {expression}, result: {result}", option);
                return result;
            }
            catch (Exception ex)
            {
                Log.Debug($"Error evaluating ternary expression: {ex.Message}, falling back to standard evaluation", option);
                // If ternary evaluation fails, try standard evaluation methods
            }
        }

        // Check for method chain pattern (e.g., "numbers.Sum()")
        var methodChainMatch = MethodChainRegex.Match(expression);
        if (methodChainMatch.Success)
        {
            string variableName = methodChainMatch.Groups[1].Value;
            string methodName = methodChainMatch.Groups[2].Value;

            // Try with custom variable resolver first for the variable name
            if (option.VariableResolver != null)
            {
                var resolvedValue = option.VariableResolver(variableName, parameter);
                if (resolvedValue != null)
                {
                    Log.Debug($"Variable resolver returned value for variable: {variableName}", option);

                    // Try to invoke the method directly on the resolved value
                    try
                    {
                        var methodInfo = resolvedValue.GetType().GetMethod(methodName, Type.EmptyTypes);
                        if (methodInfo != null)
                        {
                            var result = methodInfo.Invoke(resolvedValue, null);
                            Log.Debug($"Method chain invocation succeeded: {variableName}.{methodName}(), result: {result}", option);
                            return result;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug($"Direct method chain invocation failed: {ex.Message}, trying interpreter", option);
                    }

                    // If direct invocation fails, try with interpreter
                    try
                    {
                        // Add the resolved value to parameters for interpreter evaluation
                        parameters[variableName] = resolvedValue;

                        // Create interpreter and evaluate with the resolved variable
                        var interpreter = CreateEnhancedInterpreter(option);
                        foreach (var param in parameters)
                        {
                            interpreter = interpreter.SetVariable(param.Key, param.Value);
                        }

                        var result = interpreter.Eval(expression);
                        Log.Debug($"Interpreter evaluation of method chain succeeded: {expression}, result: {result}", option);
                        return result;
                    }
                    catch (Exception ex)
                    {
                        Log.Debug($"Interpreter method chain evaluation failed: {ex.Message}", option);
                    }
                }
            }
        }

        // Try with custom variable resolver for the whole expression
        if (option.VariableResolver != null)
        {
            var resolvedValue = option.VariableResolver(expression, parameter);
            if (resolvedValue != null)
            {
                Log.Debug($"Variable resolver returned value for expression: {expression}", option);
                return resolvedValue;
            }
        }

        // Check if the expression is a simple method invocation on the parameter object
        var methodMatch = MethodInvocationRegex.Match(expression);
        if (methodMatch.Success && parameter != null)
        {
            string methodName = methodMatch.Groups[1].Value;
            var methodInfo = parameter.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (methodInfo != null && methodInfo.GetParameters().Length == 0)
            {
                try
                {
                    var result = methodInfo.Invoke(parameter, null);
                    Log.Debug($"Method invocation succeeded: {methodName}(), result: {result}", option);
                    return result;
                }
                catch (Exception ex)
                {
                    Log.Debug($"Method invocation failed: {ex.Message}", option);
                    // Fall through to standard evaluation if method invocation fails
                }
            }
        }

        // Check if the expression is a simple variable name and exists in parameters
        if (IsSimpleVariableName(expression) && parameters.ContainsKey(expression))
        {
            var result = parameters[expression];
            Log.Debug($"Found simple variable: {expression}, value: {result}", option);
            return result;
        }

        // Check if the expression has been cached
        string cacheKey = string.Empty;
        if (option.EnableCaching)
        {
            cacheKey = $"{expression}_{parameter?.GetType().FullName ?? "null"}";
            if (_cache.TryGetLambda(cacheKey, out var cachedLambda))
            {
                try
                {
                    var result = cachedLambda.Invoke(parameter);
                    Log.Debug($"Using cached lambda for expression: {expression}, result: {result}", option);
                    return result;
                }
                catch (Exception ex)
                {
                    Log.Debug($"Error executing cached lambda: {ex.Message}", option);
                    // Continue to fresh evaluation if cached execution fails
                }
            }
        }

        try
        {
            // Add resolved variables from VariableResolver to parameters
            if (option.VariableResolver != null && methodChainMatch.Success)
            {
                string variableName = methodChainMatch.Groups[1].Value;
                if (!parameters.ContainsKey(variableName))
                {
                    var resolvedValue = option.VariableResolver(variableName, parameter);
                    if (resolvedValue != null)
                    {
                        parameters[variableName] = resolvedValue;
                        Log.Debug($"Added resolved variable {variableName} to parameters for evaluation", option);
                    }
                }
            }

            // Handle string.Join with collections specifically - this is a common source of test failures
            if (expression.Contains("string.Join") && (expression.Contains("Select") || expression.Contains("Where") || expression.Contains("OrderBy")))
            {
                if (TryHandleStringJoinWithCollections(expression, parameters, option, out var joinResult))
                {
                    // Cache the result if successful
                    if (option.EnableCaching && !string.IsNullOrEmpty(cacheKey))
                    {
                        _cache.CacheLambda(cacheKey, _ => joinResult);
                    }

                    return joinResult;
                }
            }

            // Special case handling for other patterns
            if (TryHandleSpecialCases(expression, parameters, out var specialResult, option))
            {
                Log.Debug($"Special case handler succeeded for expression: {expression}, result: {specialResult}", option);

                // Cache the result if successful
                if (option.EnableCaching && !string.IsNullOrEmpty(cacheKey))
                {
                    _cache.CacheLambda(cacheKey, _ => specialResult);
                }

                return specialResult;
            }

            // Create and configure the interpreter
            var interpreter = CreateEnhancedInterpreter(option);

            // Register all parameters with the interpreter
            foreach (var param in parameters)
            {
                interpreter = interpreter.SetVariable(param.Key, param.Value);
            }

            // Try special case for method invocation using 'this'
            if (methodMatch.Success && parameters.ContainsKey("this"))
            {
                string methodName = methodMatch.Groups[1].Value;
                try
                {
                    var result = interpreter.Eval($"this.{methodName}()");
                    Log.Debug($"Method invocation via 'this' succeeded: {methodName}(), result: {result}", option);

                    // Cache the result
                    if (option.EnableCaching && !string.IsNullOrEmpty(cacheKey))
                    {
                        _cache.CacheLambda(cacheKey, _ => result);
                    }

                    return result;
                }
                catch (Exception ex)
                {
                    Log.Debug($"Method invocation via 'this' failed: {ex.Message}", option);
                    // Continue to regular evaluation
                }
            }

            return EvaluateWithInterpreter(expression, parameter, interpreter, option, cacheKey);
        }
        catch (Exception ex)
        {
            Log.Debug($"Expression evaluation error: {ex.Message}", option);

            // Handle missing parameters
            if (option.ThrowOnMissingParameter)
                throw new DollarSignEngineException($"Error evaluating expression '{expression}': {ex.Message}", ex);

            return null;
        }
    }

    /// <summary>
    /// Checks if an expression contains a ternary operator by scanning tokens.
    /// </summary>
    private bool ContainsTernaryOperator(string expression)
    {
        // Quick check for ? and : characters
        if (!expression.Contains('?') || !expression.Contains(':'))
            return false;

        bool inString = false;
        char stringDelimiter = '\0';
        int parenthesisLevel = 0;
        bool hasQuestionMark = false;

        for (int i = 0; i < expression.Length; i++)
        {
            char current = expression[i];

            // Handle string literals
            if ((current == '"' || current == '\'') && (i == 0 || expression[i - 1] != '\\'))
            {
                if (!inString)
                {
                    inString = true;
                    stringDelimiter = current;
                }
                else if (current == stringDelimiter)
                {
                    inString = false;
                }
                continue;
            }

            // Skip characters inside string literals
            if (inString)
                continue;

            // Track parentheses nesting
            if (current == '(')
            {
                parenthesisLevel++;
            }
            else if (current == ')')
            {
                parenthesisLevel--;
            }
            else if (current == '?' && parenthesisLevel == 0)
            {
                hasQuestionMark = true;
            }
            else if (current == ':' && parenthesisLevel == 0 && hasQuestionMark)
            {
                // Found both ? and : outside of strings and parentheses
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if the expression is a simple variable name.
    /// </summary>
    private bool IsSimpleVariableName(string expression)
    {
        return SimpleVariableRegex.IsMatch(expression);
    }
}