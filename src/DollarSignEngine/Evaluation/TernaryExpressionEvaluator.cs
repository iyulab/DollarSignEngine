namespace DollarSignEngine.Evaluation;

/// <summary>
/// Handles parsing and evaluation of ternary expressions.
/// </summary>
internal class TernaryExpressionEvaluator
{
    /// <summary>
    /// Evaluates a ternary expression.
    /// </summary>
    public object? Evaluate(string expression, Dictionary<string, object?> parameters, DollarSignOptions option)
    {
        Log.Debug($"Evaluating ternary expression: {expression}", option);

        // Check for format specifier first and separate it
        string expressionWithoutFormat = expression;
        string? formatSpecifier = null;

        // Handle cases with format specifier like: (condition ? trueVal : falseVal):C2
        var formatMatch = Regex.Match(expression, @"^(.+):\s*([^:]+)$");
        if (formatMatch.Success && !formatMatch.Groups[1].Value.Contains("?"))
        {
            expressionWithoutFormat = formatMatch.Groups[1].Value;
            formatSpecifier = formatMatch.Groups[2].Value;
            Log.Debug($"Extracted format specifier: {formatSpecifier}", option);
        }

        // Parse the ternary expression using a token-based approach rather than regex
        try
        {
            var (condition, trueExpr, falseExpr) = ParseTernaryExpression(expressionWithoutFormat);
            Log.Debug($"Parsed ternary components - condition: '{condition}', trueExpr: '{trueExpr}', falseExpr: '{falseExpr}'", option);

            // Create interpreter for evaluation
            var interpreter = new DynamicExpresso.Interpreter(DynamicExpresso.InterpreterOptions.DefaultCaseInsensitive)
                .EnableReflection();

            // Register all parameters
            foreach (var param in parameters)
            {
                interpreter = interpreter.SetVariable(param.Key, param.Value);
            }

            // Evaluate the condition
            bool conditionResult;

            try
            {
                // First try with VariableResolver for the condition
                if (option.VariableResolver != null)
                {
                    var resolvedCondition = option.VariableResolver(condition, null);
                    if (resolvedCondition is bool boolValue)
                    {
                        conditionResult = boolValue;
                        Log.Debug($"Condition '{condition}' resolved to {conditionResult} by VariableResolver", option);
                    }
                    else
                    {
                        // Evaluate the condition with interpreter
                        conditionResult = Convert.ToBoolean(interpreter.Eval(condition));
                        Log.Debug($"Condition '{condition}' evaluated to {conditionResult}", option);
                    }
                }
                else
                {
                    conditionResult = Convert.ToBoolean(interpreter.Eval(condition));
                    Log.Debug($"Condition '{condition}' evaluated to {conditionResult}", option);
                }
            }
            catch (Exception ex)
            {
                Log.Debug($"Error evaluating condition '{condition}': {ex.Message}, trying direct variable lookup", option);

                // If interpreter evaluation fails, try direct variable lookup
                if (parameters.TryGetValue(condition, out var directValue) && directValue is bool directBool)
                {
                    conditionResult = directBool;
                    Log.Debug($"Found condition '{condition}' directly in parameters: {conditionResult}", option);
                }
                else
                {
                    throw;
                }
            }

            // Based on condition result, evaluate either true or false expression
            string expressionToEvaluate = conditionResult ? trueExpr : falseExpr;
            Log.Debug($"Condition is {conditionResult}, evaluating: {expressionToEvaluate}", option);

            // Handle string literals
            if ((expressionToEvaluate.StartsWith("\"") && expressionToEvaluate.EndsWith("\"")) ||
                (expressionToEvaluate.StartsWith("'") && expressionToEvaluate.EndsWith("'")))
            {
                string stringLiteral = expressionToEvaluate.Substring(1, expressionToEvaluate.Length - 2);
                stringLiteral = Regex.Unescape(stringLiteral);

                // Apply format if needed - but string literals don't need formatting
                if (!string.IsNullOrEmpty(formatSpecifier))
                {
                    Log.Debug($"Format specifier {formatSpecifier} cannot be applied to string literal", option);
                }

                return stringLiteral;
            }

            // Try VariableResolver
            if (option.VariableResolver != null)
            {
                var resolvedValue = option.VariableResolver(expressionToEvaluate, null);
                if (resolvedValue != null)
                {
                    Log.Debug($"Branch expression '{expressionToEvaluate}' resolved by VariableResolver: {resolvedValue}", option);

                    // Apply format if needed
                    if (!string.IsNullOrEmpty(formatSpecifier) && resolvedValue is IFormattable formattable)
                    {
                        var culture = option.CultureInfo ?? CultureInfo.CurrentCulture;
                        return formattable.ToString(formatSpecifier, culture);
                    }

                    return resolvedValue;
                }
            }

            // Check simple variable name
            if (IsSimpleVariableName(expressionToEvaluate) && parameters.ContainsKey(expressionToEvaluate))
            {
                var result = parameters[expressionToEvaluate];
                Log.Debug($"Found branch expression as simple variable: {expressionToEvaluate}, value: {result}", option);

                // Apply format if needed
                if (!string.IsNullOrEmpty(formatSpecifier) && result is IFormattable formattable)
                {
                    var culture = option.CultureInfo ?? CultureInfo.CurrentCulture;
                    return formattable.ToString(formatSpecifier, culture);
                }

                return result;
            }

            // Handle nested ternary operator
            if (expressionToEvaluate.Contains("?") && expressionToEvaluate.Contains(":"))
            {
                var nestedResult = Evaluate(expressionToEvaluate, parameters, option);

                // Apply format if needed
                if (!string.IsNullOrEmpty(formatSpecifier) && nestedResult is IFormattable formattable)
                {
                    var culture = option.CultureInfo ?? CultureInfo.CurrentCulture;
                    return formattable.ToString(formatSpecifier, culture);
                }

                return nestedResult;
            }

            // Otherwise evaluate with interpreter
            try
            {
                var result = interpreter.Eval(expressionToEvaluate);
                Log.Debug($"Branch expression '{expressionToEvaluate}' evaluated to: {result}", option);

                // Apply format if needed
                if (!string.IsNullOrEmpty(formatSpecifier) && result is IFormattable formattable)
                {
                    var culture = option.CultureInfo ?? CultureInfo.CurrentCulture;
                    return formattable.ToString(formatSpecifier, culture);
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.Debug($"Error evaluating branch expression '{expressionToEvaluate}': {ex.Message}", option);

                // Special handling for method calls
                if (expressionToEvaluate.Contains("(") && expressionToEvaluate.Contains(")"))
                {
                    var methodMatch = Regex.Match(expressionToEvaluate, @"^(\w+)\(\)$");
                    if (methodMatch.Success && parameters.ContainsKey("this"))
                    {
                        string methodName = methodMatch.Groups[1].Value;
                        var thisObj = parameters["this"];

                        if (thisObj != null)
                        {
                            try
                            {
                                var methodInfo = thisObj.GetType().GetMethod(methodName, Type.EmptyTypes);
                                if (methodInfo != null)
                                {
                                    var methodResult = methodInfo.Invoke(thisObj, null);
                                    Log.Debug($"Method invocation in branch succeeded: {methodName}(), result: {methodResult}", option);

                                    // Apply format if needed
                                    if (!string.IsNullOrEmpty(formatSpecifier) && methodResult is IFormattable formattable)
                                    {
                                        var culture = option.CultureInfo ?? CultureInfo.CurrentCulture;
                                        return formattable.ToString(formatSpecifier, culture);
                                    }

                                    return methodResult;
                                }
                            }
                            catch (Exception methodEx)
                            {
                                Log.Debug($"Method invocation in branch failed: {methodEx.Message}", option);
                            }
                        }
                    }
                }

                throw;
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"Error parsing or evaluating ternary expression '{expression}': {ex.Message}", option);
            throw;
        }
    }

    /// <summary>
    /// Parses a ternary expression into condition, true expression, and false expression.
    /// Uses a token-based approach for more accurate parsing of nested expressions.
    /// </summary>
    private (string Condition, string TrueExpression, string FalseExpression) ParseTernaryExpression(string expression)
    {
        // Handle parentheses in the expression
        expression = expression.Trim();

        // Remove outer parentheses if present
        if (expression.StartsWith("(") && expression.EndsWith(")") &&
            IsBalancedParentheses(expression.Substring(1, expression.Length - 2)))
        {
            expression = expression.Substring(1, expression.Length - 2).Trim();
        }

        // Find the positions of ? and : operators at the top level
        int questionPos = -1;
        int colonPos = -1;
        int depth = 0;
        bool inString = false;
        char stringDelimiter = '\0';

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

            // Skip characters inside string literals
            if (inString)
                continue;

            // Track parentheses nesting
            if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth--;
            }
            // Find the first ? at the top level
            else if (c == '?' && depth == 0 && questionPos == -1)
            {
                questionPos = i;
            }
            // Find the first : after ? at the top level
            else if (c == ':' && depth == 0 && questionPos != -1 && colonPos == -1)
            {
                colonPos = i;
            }
        }

        // Validate we found both operators
        if (questionPos == -1 || colonPos == -1)
        {
            throw new FormatException("Invalid ternary expression format. Expected '?' and ':' operators.");
        }

        // Extract the three parts
        string condition = expression.Substring(0, questionPos).Trim();
        string trueExpression = expression.Substring(questionPos + 1, colonPos - questionPos - 1).Trim();
        string falseExpression = expression.Substring(colonPos + 1).Trim();

        return (condition, trueExpression, falseExpression);
    }

    /// <summary>
    /// Checks if a string has balanced parentheses.
    /// </summary>
    private bool IsBalancedParentheses(string expression)
    {
        int depth = 0;
        bool inString = false;
        char stringDelimiter = '\0';

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

            // Skip characters inside string literals
            if (inString)
                continue;

            if (c == '(')
                depth++;
            else if (c == ')')
                depth--;

            // If at any point depth goes negative, we have unbalanced parentheses
            if (depth < 0)
                return false;
        }

        // Return true if we've finished with a depth of 0 (all opened parentheses are closed)
        return depth == 0;
    }

    /// <summary>
    /// Checks if an expression is a simple variable name.
    /// </summary>
    private bool IsSimpleVariableName(string expression)
    {
        return Regex.IsMatch(expression, @"^[a-zA-Z_][a-zA-Z0-9_]*$");
    }
}