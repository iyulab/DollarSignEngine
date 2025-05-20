namespace DollarSignEngine.Internals;

/// <summary>
/// Wraps a dictionary to allow its keys to be accessed as dynamic properties.
/// </summary>
public class DictionaryWrapper : DynamicObject
{
    private readonly IDictionary<string, object?> _dictionary;

    /// <summary>
    /// Creates a new DictionaryWrapper around the specified dictionary.
    /// </summary>
    public DictionaryWrapper(IDictionary<string, object?> dictionary)
    {
        _dictionary = dictionary ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the underlying dictionary.
    /// </summary>
    public IDictionary<string, object?> Dictionary => _dictionary;

    /// <summary>
    /// Attempts to get a member value dynamically.
    /// </summary>
    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        // First try exact case match
        if (_dictionary.TryGetValue(binder.Name, out result))
        {
            return true;
        }

        // If not found with exact case, try case-insensitive lookup
        foreach (var key in _dictionary.Keys)
        {
            if (string.Equals(key, binder.Name, StringComparison.OrdinalIgnoreCase))
            {
                result = _dictionary[key];
                return true;
            }
        }

        result = null;
        return false;
    }

    /// <summary>
    /// Attempts to set a member value dynamically.
    /// </summary>
    public override bool TrySetMember(SetMemberBinder binder, object? value)
    {
        // Check if any key with the same name but different case exists
        string? existingKey = null;
        foreach (var key in _dictionary.Keys)
        {
            if (string.Equals(key, binder.Name, StringComparison.OrdinalIgnoreCase))
            {
                existingKey = key;
                break;
            }
        }

        // Update existing key if found, otherwise add new key
        if (existingKey != null)
        {
            _dictionary[existingKey] = value;
        }
        else
        {
            _dictionary[binder.Name] = value;
        }

        return true;
    }

    /// <summary>
    /// Tries to get a value from the dictionary using string key.
    /// </summary>
    public object? TryGetValue(string key)
    {
        // First try exact case match
        if (_dictionary.TryGetValue(key, out var value))
        {
            return value;
        }

        // If not found with exact case, try case-insensitive lookup
        foreach (var dictKey in _dictionary.Keys)
        {
            if (string.Equals(dictKey, key, StringComparison.OrdinalIgnoreCase))
            {
                return _dictionary[dictKey];
            }
        }

        return null;
    }

    /// <summary>
    /// Determines whether the dictionary contains a specific key.
    /// </summary>
    public bool ContainsKey(string key)
    {
        // First try exact case match
        if (_dictionary.ContainsKey(key))
        {
            return true;
        }

        // Try case-insensitive lookup
        foreach (var dictKey in _dictionary.Keys)
        {
            if (string.Equals(dictKey, key, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets an enumeration of all dynamic member names.
    /// </summary>
    public override IEnumerable<string> GetDynamicMemberNames()
    {
        return _dictionary.Keys;
    }

    /// <summary>
    /// Returns an enumerator that iterates through the dictionary.
    /// </summary>
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        return _dictionary.GetEnumerator();
    }
}