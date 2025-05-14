namespace DollarSignEngine.Evaluation;

/// <summary>
/// Special handler for ternary operations in expressions.
/// </summary>
internal partial class ExpressionEvaluator
{
    // Regular expression for basic ternary operations
    private static readonly Regex TernaryOperatorRegex = new(@"^(.+?)\s*\?\s*(.+?)\s*:\s*(.+)$", RegexOptions.Compiled);
    // Regular expression for ternary operations with parentheses
    private static readonly Regex TernaryWithParenthesesRegex = new(@"^(.*?\(.+?\).*?)\s*\?\s*(.+?)\s*:\s*(.+)$", RegexOptions.Compiled);

    /// <summary>
    /// Handles ternary operator expressions.
    /// </summary>
    private bool TryHandleTernaryOperator(string expression, Dictionary<string, object?> parameters, DollarSignOptions option, out object? result)
    {
        result = null;

        // Try to handle ternary operators directly
        var ternaryMatch = TernaryOperatorRegex.Match(expression);
        if (!ternaryMatch.Success)
        {
            ternaryMatch = TernaryWithParenthesesRegex.Match(expression);
            if (!ternaryMatch.Success)
                return false;
        }

        string condition = ternaryMatch.Groups[1].Value.Trim();
        string trueExpression = ternaryMatch.Groups[2].Value.Trim();
        string falseExpression = ternaryMatch.Groups[3].Value.Trim();

        Log.Debug($"Detected ternary operator: condition='{condition}', trueExpr='{trueExpression}', falseExpr='{falseExpression}'", option);

        try
        {
            // Create interpreter for condition evaluation
            var interpreter = CreateEnhancedInterpreter(option);
            foreach (var param in parameters)
            {
                interpreter = interpreter.SetVariable(param.Key, param.Value);
            }

            // Try to evaluate the condition
            bool? conditionResult = null;

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
                }

                // If condition wasn't resolved, evaluate it with interpreter
                if (conditionResult == null)
                {
                    var evaluatedCondition = interpreter.Eval(condition);
                    if (evaluatedCondition is bool boolResult)
                    {
                        conditionResult = boolResult;
                        Log.Debug($"Condition '{condition}' evaluated to {conditionResult}", option);
                    }
                    else
                    {
                        // Try to convert to boolean if possible
                        conditionResult = Convert.ToBoolean(evaluatedCondition);
                        Log.Debug($"Condition '{condition}' converted to {conditionResult}", option);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug($"Error evaluating condition '{condition}': {ex.Message}", option);
                return false;
            }

            // Based on condition result, evaluate either true or false expression
            string expressionToEvaluate = conditionResult.Value ? trueExpression : falseExpression;
            Log.Debug($"Condition is {conditionResult}, evaluating: {expressionToEvaluate}", option);

            try
            {
                // First check if the expression is a string literal
                if ((expressionToEvaluate.StartsWith("\"") && expressionToEvaluate.EndsWith("\"")) ||
                    (expressionToEvaluate.StartsWith("'") && expressionToEvaluate.EndsWith("'")))
                {
                    // Remove quotes and unescape
                    string stringLiteral = expressionToEvaluate.Substring(1, expressionToEvaluate.Length - 2);
                    stringLiteral = Regex.Unescape(stringLiteral);
                    result = stringLiteral;
                    Log.Debug($"Evaluated string literal: {result}", option);
                    return true;
                }

                // Try VariableResolver first for the branch expression
                if (option.VariableResolver != null)
                {
                    var resolvedValue = option.VariableResolver(expressionToEvaluate, null);
                    if (resolvedValue != null)
                    {
                        result = resolvedValue;
                        Log.Debug($"Branch expression '{expressionToEvaluate}' resolved by VariableResolver: {result}", option);
                        return true;
                    }
                }

                // If it's a simple variable name and exists in parameters
                if (IsSimpleVariableName(expressionToEvaluate) && parameters.ContainsKey(expressionToEvaluate))
                {
                    result = parameters[expressionToEvaluate];
                    Log.Debug($"Found branch expression as simple variable: {expressionToEvaluate}, value: {result}", option);
                    return true;
                }

                // Otherwise, evaluate with interpreter
                result = interpreter.Eval(expressionToEvaluate);
                Log.Debug($"Branch expression '{expressionToEvaluate}' evaluated to: {result}", option);
                return true;
            }
            catch (Exception ex)
            {
                Log.Debug($"Error evaluating branch expression '{expressionToEvaluate}': {ex.Message}", option);

                // Special handling for method calls in branch expressions
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
                                    result = methodInfo.Invoke(thisObj, null);
                                    Log.Debug($"Method invocation in branch succeeded: {methodName}(), result: {result}", option);
                                    return true;
                                }
                            }
                            catch (Exception methodEx)
                            {
                                Log.Debug($"Method invocation in branch failed: {methodEx.Message}", option);
                            }
                        }
                    }
                }

                return false;
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"Error handling ternary operator: {ex.Message}", option);
            return false;
        }
    }
}