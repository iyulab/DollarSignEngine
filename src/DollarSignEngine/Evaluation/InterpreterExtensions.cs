using DynamicExpresso;

namespace DollarSignEngine.Evaluation;

// Extension method for setting multiple variables at once
internal static class InterpreterExtensions
{
    /// <summary>
    /// Sets multiple variables on an interpreter at once.
    /// </summary>
    public static Interpreter SetVariables(this Interpreter interpreter, Dictionary<string, object?> variables)
    {
        foreach (var variable in variables)
        {
            interpreter = interpreter.SetVariable(variable.Key, variable.Value);
        }
        return interpreter;
    }
}