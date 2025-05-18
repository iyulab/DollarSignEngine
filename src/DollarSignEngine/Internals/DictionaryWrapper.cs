using System.Dynamic;

namespace DollarSignEngine.Internals;

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
        return _dictionary.TryGetValue(binder.Name, out result);
    }

    public override bool TrySetMember(SetMemberBinder binder, object? value)
    {
        _dictionary[binder.Name] = value;
        return true;
    }

    /// <summary>
    /// Tries to get a value from the underlying dictionary.
    /// </summary>
    public object? TryGetValue(string key)
    {
        return _dictionary.TryGetValue(key, out var v) ? v : null;
    }

    public override IEnumerable<string> GetDynamicMemberNames()
    {
        return _dictionary.Keys;
    }
}