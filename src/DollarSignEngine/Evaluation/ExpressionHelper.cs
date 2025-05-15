using DynamicExpresso;

namespace DollarSignEngine.Evaluation;

/// <summary>
/// Provides specialized evaluation techniques for complex expressions.
/// </summary>
internal static class ExpressionHelper
{
    /// <summary>
    /// Attempts multiple strategies to evaluate an expression with DynamicExpresso
    /// </summary>
    public static object? TryEvaluateExpression(Interpreter interpreter, string expression, DollarSignOptions options)
    {
        Log.Debug($"Attempting to evaluate expression: {expression}", options);

        try
        {
            // Strategy 1: Direct evaluation
            try
            {
                var result = interpreter.Eval(expression);
                Log.Debug($"Direct evaluation succeeded: {result}", options);
                return result;
            }
            catch (Exception ex)
            {
                Log.Debug($"Direct evaluation failed: {ex.Message}", options);
            }

            // Strategy 2: Try wrapping in lambda
            try
            {
                var lambda = interpreter.Parse<Func<object>>("() => " + expression);
                var result = lambda();
                Log.Debug($"Lambda evaluation succeeded: {result}", options);
                return result;
            }
            catch (Exception ex)
            {
                Log.Debug($"Lambda evaluation failed: {ex.Message}", options);
            }

            // Strategy 3: Try handling as a complex expression by breaking it down
            if (expression.Contains("?") && expression.Contains(":"))
            {
                try
                {
                    var parts = ParseTernary(expression);
                    bool condition = Convert.ToBoolean(interpreter.Eval(parts.condition));

                    if (condition)
                    {
                        var result = interpreter.Eval(parts.trueExpr);
                        Log.Debug($"Ternary condition true, evaluated true expression: {result}", options);
                        return result;
                    }
                    else
                    {
                        var result = interpreter.Eval(parts.falseExpr);
                        Log.Debug($"Ternary condition false, evaluated false expression: {result}", options);
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug($"Ternary evaluation failed: {ex.Message}", options);
                }
            }

            // If we get here, all evaluation strategies failed
            throw new DollarSignEngineException($"Could not evaluate expression: {expression}");
        }
        catch (Exception ex)
        {
            Log.Debug($"All evaluation strategies failed: {ex.Message}", options);
            throw;
        }
    }

    /// <summary>
    /// Parses a ternary expression into its condition, true expression, and false expression parts
    /// </summary>
    public static (string condition, string trueExpr, string falseExpr) ParseTernary(string expression)
    {
        // Find the ? that's not inside a string, parenthesis, etc.
        int questionIndex = FindOperatorIndex(expression, '?');
        if (questionIndex < 0)
        {
            throw new ArgumentException("Not a valid ternary expression - no '?' found");
        }

        // Find the : that corresponds to our ? (not inside a string, not for a nested ternary)
        int colonIndex = FindMatchingColonIndex(expression, questionIndex);
        if (colonIndex < 0)
        {
            throw new ArgumentException("Not a valid ternary expression - no matching ':' found");
        }

        // Extract the parts
        string condition = expression.Substring(0, questionIndex).Trim();
        string trueExpr = expression.Substring(questionIndex + 1, colonIndex - questionIndex - 1).Trim();
        string falseExpr = expression.Substring(colonIndex + 1).Trim();

        return (condition, trueExpr, falseExpr);
    }

    /// <summary>
    /// Finds the index of an operator that's not inside a string literal, parenthesis, etc.
    /// </summary>
    private static int FindOperatorIndex(string expression, char target)
    {
        bool inString = false;
        char stringDelimiter = '\0';
        int parenDepth = 0;
        int bracketDepth = 0;
        int braceDepth = 0;

        for (int i = 0; i < expression.Length; i++)
        {
            char c = expression[i];

            // Handle strings
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
            else if (c == '{') braceDepth++;
            else if (c == '}') braceDepth--;

            // Check for target when we're not in any nested context
            else if (c == target && parenDepth == 0 && bracketDepth == 0 && braceDepth == 0)
            {
                // Special handling for ? to avoid confusing with ??
                if (target == '?' && i < expression.Length - 1 && expression[i + 1] == '?')
                {
                    i++; // Skip the second ? in ??
                    continue;
                }
                return i;
            }
        }

        return -1; // Not found
    }

    /// <summary>
    /// Finds the colon that matches a given question mark in a ternary expression
    /// </summary>
    private static int FindMatchingColonIndex(string expression, int questionIndex)
    {
        bool inString = false;
        char stringDelimiter = '\0';
        int parenDepth = 0;
        int bracketDepth = 0;
        int braceDepth = 0;
        int nestedQuestionCount = 0;

        for (int i = questionIndex + 1; i < expression.Length; i++)
        {
            char c = expression[i];

            // Handle strings
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
            else if (c == '{') braceDepth++;
            else if (c == '}') braceDepth--;

            // Handle nested questions
            else if (c == '?' && parenDepth == 0 && bracketDepth == 0 && braceDepth == 0)
            {
                // Skip ?? operator
                if (i < expression.Length - 1 && expression[i + 1] == '?')
                {
                    i++;
                    continue;
                }
                nestedQuestionCount++;
            }

            // Handle colons
            else if (c == ':' && parenDepth == 0 && bracketDepth == 0 && braceDepth == 0)
            {
                if (nestedQuestionCount == 0)
                {
                    return i;
                }
                nestedQuestionCount--;
            }
        }

        return -1; // Not found
    }
}