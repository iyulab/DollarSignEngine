using DynamicExpresso;

namespace DollarSignEngine.Evaluation;

/// <summary>
/// Extension methods for DynamicExpresso Interpreter.
/// </summary>
internal static class InterpreterExtensions
{
    /// <summary>
    /// Sets multiple variables on an interpreter at once.
    /// </summary>
    public static Interpreter SetVariables(this Interpreter interpreter, Dictionary<string, object?> variables)
    {
        foreach (var variable in variables)
        {
            try
            {
                interpreter = interpreter.SetVariable(variable.Key, variable.Value);
            }
            catch
            {
                // Skip variables that can't be set
            }
        }
        return interpreter;
    }

    /// <summary>
    /// Adds common system references to an interpreter.
    /// </summary>
    public static Interpreter AddCommonReferences(this Interpreter interpreter)
    {
        return interpreter
            .Reference(typeof(Enumerable))
            .Reference(typeof(List<>))
            .Reference(typeof(Dictionary<,>))
            .Reference(typeof(DateTime))
            .Reference(typeof(DateTimeOffset))
            .Reference(typeof(TimeSpan))
            .Reference(typeof(string))
            .Reference(typeof(Regex))
            .Reference(typeof(Queryable))
            .Reference(typeof(IQueryable<>))
            .Reference(typeof(IEnumerable<>))
            .Reference(typeof(ICollection<>))
            .Reference(typeof(Path))
            .Reference(typeof(StringComparer))
            .Reference(typeof(Convert))
            .Reference(typeof(Math))
            .Reference(typeof(Guid))
            .Reference(typeof(StringBuilder));
    }

    /// <summary>
    /// Creates a delegate that wraps the provided object and expression.
    /// </summary>
    public static Func<object?, object?> CreateExpressionDelegate(this Interpreter interpreter, string expression, Dictionary<string, object?> parameters)
    {
        // Set all parameters to the interpreter
        interpreter = interpreter.SetVariables(parameters);

        // Parse the expression
        var lambda = interpreter.Parse(expression);

        // Return a delegate that will evaluate this expression with the provided parameters
        return _ => lambda.Invoke();
    }
}