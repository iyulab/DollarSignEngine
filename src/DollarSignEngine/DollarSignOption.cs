namespace DollarSignEngine;

/// <summary>
/// Options for configuring the behavior of the DollarSign engine.
/// </summary>
public class DollarSignOption
{
    /// <summary>
    /// Gets or sets a value indicating whether to throw an exception when a parameter is missing.
    /// </summary>
    public bool ThrowOnMissingParameter { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to enable debug logging.
    /// </summary>
    public bool EnableDebugLogging { get; set; } = false;

    /// <summary>
    /// Gets or sets additional namespaces to import in the script.
    /// </summary>
    public List<string> AdditionalNamespaces { get; set; } = [];

    /// <summary>
    /// Gets or sets additional assemblies to reference in the script.
    /// </summary>
    public List<string> AdditionalAssemblies { get; set; } = [];
}