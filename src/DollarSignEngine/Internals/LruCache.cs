namespace DollarSignEngine.Internals;

/// <summary>
/// Thread-safe LRU (Least Recently Used) cache implementation with fixed capacity.
/// </summary>
internal class LruCache<TKey, TValue> where TKey : notnull
{
    private readonly int _capacity;
    private readonly Dictionary<TKey, LinkedListNode<LruCacheItem>> _cacheMap;
    private readonly LinkedList<LruCacheItem> _lruList;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the LruCache class.
    /// </summary>
    public LruCache(int capacity, IEqualityComparer<TKey>? comparer = null)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");

        _capacity = capacity;
        _cacheMap = new Dictionary<TKey, LinkedListNode<LruCacheItem>>(capacity, comparer);
        _lruList = new LinkedList<LruCacheItem>();
    }

    /// <summary>
    /// Gets the current count of items in the cache.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _cacheMap.Count;
            }
        }
    }

    /// <summary>
    /// Retrieves a value from the cache, or adds it if not present.
    /// </summary>
    public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
    {
        lock (_lock)
        {
            if (_cacheMap.TryGetValue(key, out var node))
            {
                // Move existing item to front (most recently used)
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                return node.Value.Value;
            }

            TValue value = valueFactory(key);

            // Evict least recently used item if cache is full
            if (_cacheMap.Count >= _capacity)
            {
                LinkedListNode<LruCacheItem>? last = _lruList.Last;
                if (last != null)
                {
                    _cacheMap.Remove(last.Value.Key);
                    _lruList.RemoveLast();
                }
            }

            // Add new item at front
            var cacheItem = new LruCacheItem(key, value);
            var newNode = new LinkedListNode<LruCacheItem>(cacheItem);
            _lruList.AddFirst(newNode);
            _cacheMap.Add(key, newNode);

            return value;
        }
    }

    /// <summary>
    /// Checks if the cache contains the specified key.
    /// </summary>
    public bool Contains(TKey key)
    {
        lock (_lock)
        {
            return _cacheMap.ContainsKey(key);
        }
    }

    /// <summary>
    /// Attempts to get a value from the cache.
    /// </summary>
    public bool TryGetValue(TKey key, out TValue value)
    {
        lock (_lock)
        {
            if (_cacheMap.TryGetValue(key, out var node))
            {
                // Move to front (most recently used)
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                value = node.Value.Value;
                return true;
            }

            value = default!;
            return false;
        }
    }

    /// <summary>
    /// Removes an item from the cache.
    /// </summary>
    public bool Remove(TKey key)
    {
        lock (_lock)
        {
            if (_cacheMap.TryGetValue(key, out var node))
            {
                _lruList.Remove(node);
                _cacheMap.Remove(key);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Clears all items from the cache.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _cacheMap.Clear();
            _lruList.Clear();
        }
    }

    // Internal class to track cache items
    private class LruCacheItem
    {
        public TKey Key { get; }
        public TValue Value { get; }

        public LruCacheItem(TKey key, TValue value)
        {
            Key = key;
            Value = value;
        }
    }
}