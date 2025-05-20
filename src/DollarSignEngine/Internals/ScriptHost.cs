namespace DollarSignEngine.Internals;

/// <summary>
/// Host class for C# script evaluation that provides a container for global variables.
/// </summary>
public class ScriptHost
{
    /// <summary>
    /// The dictionary of global variables accessible to scripts.
    /// </summary>
    public IDictionary<string, object?> Globals { get; }

    /// <summary>
    /// Creates a new ScriptHost with the specified global variables.
    /// </summary>
    /// <param name="globals">Dictionary of global variables to be available in scripts.</param>
    public ScriptHost(IDictionary<string, object?> globals)
    {
        Globals = globals ?? new Dictionary<string, object?>();
    }
}