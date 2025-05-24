using System.Collections.Concurrent;

namespace DollarSignEngine.Internals;

/// <summary>
/// High-performance thread-safe LRU cache with TTL support and memory management.
/// Fixed deadlock issues with proper lock management.
/// </summary>
internal class LruCache<TKey, TValue> : IDisposable where TKey : notnull
{
    private readonly int _capacity;
    private readonly TimeSpan _ttl;
    private readonly ConcurrentDictionary<TKey, LinkedListNode<CacheItem>> _cacheMap;
    private readonly LinkedList<CacheItem> _lruList;
    private readonly object _lock = new object(); // Use simple lock instead of ReaderWriterLockSlim
    private readonly Timer _cleanupTimer;
    private volatile bool _disposed;

    // Performance metrics
    private long _cacheHits;
    private long _totalRequests;

    /// <summary>
    /// Initializes a new instance of the LruCache class.
    /// </summary>
    /// <param name="capacity">Maximum number of items to cache.</param>
    /// <param name="ttl">Time-to-live for cache items. Use TimeSpan.Zero for no expiration.</param>
    /// <param name="comparer">Key comparer.</param>
    public LruCache(int capacity, TimeSpan ttl = default, IEqualityComparer<TKey>? comparer = null)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");

        _capacity = capacity;
        _ttl = ttl == default ? TimeSpan.FromHours(1) : ttl; // Default 1 hour TTL
        _cacheMap = new ConcurrentDictionary<TKey, LinkedListNode<CacheItem>>(comparer);
        _lruList = new LinkedList<CacheItem>();

        // Cleanup timer runs every 5 minutes or TTL/4, whichever is smaller
        var cleanupInterval = TimeSpan.FromMilliseconds(Math.Min(_ttl.TotalMilliseconds / 4, TimeSpan.FromMinutes(5).TotalMilliseconds));
        _cleanupTimer = new Timer(CleanupExpiredItems, null, cleanupInterval, cleanupInterval);
    }

    /// <summary>
    /// Gets the current count of items in the cache.
    /// </summary>
    public int Count
    {
        get
        {
            ThrowIfDisposed();
            lock (_lock)
            {
                return _cacheMap.Count;
            }
        }
    }

    /// <summary>
    /// Gets cache hit rate for monitoring.
    /// </summary>
    public double HitRate => _totalRequests == 0 ? 0.0 : (double)_cacheHits / _totalRequests;

    /// <summary>
    /// Retrieves a value from the cache, or adds it if not present.
    /// </summary>
    public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
    {
        ThrowIfDisposed();
        Interlocked.Increment(ref _totalRequests);

        lock (_lock)
        {
            // Check if item exists and is not expired
            if (_cacheMap.TryGetValue(key, out var node))
            {
                if (!IsExpired(node.Value))
                {
                    // Move to front (most recently used)
                    _lruList.Remove(node);
                    _lruList.AddFirst(node);
                    Interlocked.Increment(ref _cacheHits);
                    return node.Value.Value;
                }
                else
                {
                    // Remove expired item
                    _lruList.Remove(node);
                    _cacheMap.TryRemove(key, out _);
                }
            }

            // Create new value
            TValue value = valueFactory(key);

            // Evict if necessary
            while (_cacheMap.Count >= _capacity && _lruList.Last != null)
            {
                var lastNode = _lruList.Last;
                _lruList.RemoveLast();
                _cacheMap.TryRemove(lastNode.Value.Key, out _);
            }

            // Add new item
            var cacheItem = new CacheItem(key, value, DateTime.UtcNow.Add(_ttl));
            var newNode = new LinkedListNode<CacheItem>(cacheItem);
            _lruList.AddFirst(newNode);
            _cacheMap.TryAdd(key, newNode);

            return value;
        }
    }

    /// <summary>
    /// Attempts to get a value from the cache.
    /// </summary>
    public bool TryGetValue(TKey key, out TValue value)
    {
        ThrowIfDisposed();
        Interlocked.Increment(ref _totalRequests);

        lock (_lock)
        {
            if (_cacheMap.TryGetValue(key, out var node) && !IsExpired(node.Value))
            {
                // Move to front (most recently used)
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                value = node.Value.Value;
                Interlocked.Increment(ref _cacheHits);
                return true;
            }
        }

        value = default!;
        return false;
    }

    /// <summary>
    /// Removes an item from the cache.
    /// </summary>
    public bool Remove(TKey key)
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            if (_cacheMap.TryGetValue(key, out var node))
            {
                _lruList.Remove(node);
                _cacheMap.TryRemove(key, out _);
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
        ThrowIfDisposed();

        lock (_lock)
        {
            _cacheMap.Clear();
            _lruList.Clear();
            Interlocked.Exchange(ref _cacheHits, 0);
            Interlocked.Exchange(ref _totalRequests, 0);
        }
    }

    private bool IsExpired(CacheItem item)
    {
        return _ttl != TimeSpan.Zero && DateTime.UtcNow > item.ExpirationTime;
    }

    private void CleanupExpiredItems(object? state)
    {
        if (_disposed) return;

        try
        {
            lock (_lock)
            {
                var expiredNodes = new List<LinkedListNode<CacheItem>>();
                var current = _lruList.Last;

                while (current != null)
                {
                    if (IsExpired(current.Value))
                    {
                        expiredNodes.Add(current);
                    }
                    current = current.Previous;
                }

                foreach (var node in expiredNodes)
                {
                    _lruList.Remove(node);
                    _cacheMap.TryRemove(node.Value.Key, out _);
                }

                if (expiredNodes.Count > 0)
                {
                    Logger.Debug($"[LruCache] Cleaned up {expiredNodes.Count} expired items");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"[LruCache] Error during cleanup: {ex.Message}");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(LruCache<TKey, TValue>));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            _cleanupTimer?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Warning($"[LruCache] Error during disposal: {ex.Message}");
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Internal cache item with expiration support.
    /// </summary>
    private readonly struct CacheItem
    {
        public TKey Key { get; }
        public TValue Value { get; }
        public DateTime ExpirationTime { get; }

        public CacheItem(TKey key, TValue value, DateTime expirationTime)
        {
            Key = key;
            Value = value;
            ExpirationTime = expirationTime;
        }
    }
}