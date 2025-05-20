namespace DollarSignEngine.Internals; // Adjust namespace if needed

/// <summary>
/// Wraps a dictionary to allow its keys to be accessed as dynamic properties.
/// </summary>
public class DictionaryWrapper : DynamicObject
{
    private readonly IDictionary<string, object?> _dictionary;

    public DictionaryWrapper(IDictionary<string, object?> dictionary)
    {
        _dictionary = dictionary ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    }

    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        // First try exact case match
        if (_dictionary.TryGetValue(binder.Name, out result))
        {
            return true;
        }

        // If not found with exact case, try case-insensitive lookup manually
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
    /// Tries to get a value from the underlying dictionary.
    /// </summary>
    public object? TryGetValue(string key)
    {
        // First try exact case match
        if (_dictionary.TryGetValue(key, out var value))
        {
            return value;
        }

        // If not found with exact case, try case-insensitive lookup manually
        foreach (var dictKey in _dictionary.Keys)
        {
            if (string.Equals(dictKey, key, StringComparison.OrdinalIgnoreCase))
            {
                return _dictionary[dictKey];
            }
        }

        return null;
    }

    public override IEnumerable<string> GetDynamicMemberNames()
    {
        return _dictionary.Keys;
    }
}