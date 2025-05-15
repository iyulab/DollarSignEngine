using System.Text.RegularExpressions;

namespace DollarSignEngine.Evaluation;

/// <summary>
/// Evaluates ternary expressions using a specialized approach to overcome DynamicExpresso limitations.
/// </summary>
internal class TernaryExpressionEvaluator
{
    private readonly ExpressionEvaluator _expressionEvaluator;

    // Regex for detecting potential ternary expressions - looks for ? followed by :
    private static readonly Regex TernaryRegex = new(@".*?\?.*?:.*", RegexOptions.Compiled);

    /// <summary>
    /// Initializes a new instance of the TernaryExpressionEvaluator class.
    /// </summary>
    public TernaryExpressionEvaluator(ExpressionEvaluator expressionEvaluator)
    {
        _expressionEvaluator = expressionEvaluator;
    }

    /// <summary>
    /// Determines if an expression is a ternary expression.
    /// </summary>
    public static bool IsTernaryExpression(string expression)
    {
        // Quick check for the presence of both ? and : operators
        if (!expression.Contains('?') || !expression.Contains(':'))
            return false;

        // Use regex for a faster initial check
        if (!TernaryRegex.IsMatch(expression))
            return false;

        // Try to parse the ternary expression to verify it's valid
        try
        {
            var parts = ExpressionHelper.ParseTernary(expression);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Evaluates a ternary expression by manually parsing and evaluating its components.
    /// </summary>
    public object? EvaluateTernary(string expression, object? parameter, DollarSignOptions options)
    {
        Log.Debug($"Evaluating ternary expression: {expression}", options);

        try
        {
            // Parse the ternary expression into its parts
            var (condition, trueExpression, falseExpression) = ExpressionHelper.ParseTernary(expression);

            Log.Debug($"Parsed ternary components: condition='{condition}', true='{trueExpression}', false='{falseExpression}'", options);

            // Evaluate the condition
            var conditionResult = _expressionEvaluator.Evaluate(condition, parameter, options);
            bool conditionValue;

            // Handle null condition or convert to boolean
            if (conditionResult == null)
            {
                conditionValue = false;
            }
            else if (conditionResult is bool b)
            {
                conditionValue = b;
            }
            else
            {
                try
                {
                    conditionValue = Convert.ToBoolean(conditionResult);
                }
                catch (Exception ex)
                {
                    throw new DollarSignEngineException($"Could not convert ternary condition result to boolean: {ex.Message}", ex);
                }
            }

            Log.Debug($"Condition evaluated to: {conditionValue}", options);

            // Based on the condition result, evaluate either the true or false expression
            if (conditionValue)
            {
                var result = _expressionEvaluator.Evaluate(trueExpression, parameter, options);
                Log.Debug($"True expression '{trueExpression}' evaluated to: {result}", options);
                return result;
            }
            else
            {
                var result = _expressionEvaluator.Evaluate(falseExpression, parameter, options);
                Log.Debug($"False expression '{falseExpression}' evaluated to: {result}", options);
                return result;
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"Error evaluating ternary expression: {ex.Message}", options);
            throw new DollarSignEngineException($"Error evaluating ternary expression '{expression}': {ex.Message}", ex);
        }
    }
}