namespace DollarSignEngine.Internals;

/// <summary>
/// Host class for C# script evaluation that provides a container for global variables.
/// Enhanced to handle null variables properly.
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
        Globals = globals ?? new NullSafeGlobalsDictionary();
    }

    /// <summary>
    /// Special dictionary implementation that handles null variable access gracefully.
    /// When a variable is not found, it returns null instead of throwing an exception.
    /// This mimics C# string interpolation behavior.
    /// </summary>
    private class NullSafeGlobalsDictionary : IDictionary<string, object?>
    {
        private readonly Dictionary<string, object?> _inner = new(StringComparer.OrdinalIgnoreCase);

        public object? this[string key]
        {
            get => _inner.TryGetValue(key, out var value) ? value : null;
            set => _inner[key] = value;
        }

        public ICollection<string> Keys => _inner.Keys;
        public ICollection<object?> Values => _inner.Values;
        public int Count => _inner.Count;
        public bool IsReadOnly => false;

        public void Add(string key, object? value) => _inner.Add(key, value);
        public void Add(KeyValuePair<string, object?> item) => _inner.Add(item.Key, item.Value);
        public void Clear() => _inner.Clear();
        public bool Contains(KeyValuePair<string, object?> item) => _inner.Contains(item);
        public bool ContainsKey(string key) => _inner.ContainsKey(key);
        public void CopyTo(KeyValuePair<string, object?>[] array, int arrayIndex) =>
            ((ICollection<KeyValuePair<string, object?>>)_inner).CopyTo(array, arrayIndex);
        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _inner.GetEnumerator();
        public bool Remove(string key) => _inner.Remove(key);
        public bool Remove(KeyValuePair<string, object?> item) => ((ICollection<KeyValuePair<string, object?>>)_inner).Remove(item);

        public bool TryGetValue(string key, out object? value)
        {
            if (_inner.TryGetValue(key, out value))
                return true;

            // For missing keys, return null but indicate success
            // This prevents KeyNotFoundException during script execution
            value = null;
            return true;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}