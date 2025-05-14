namespace DollarSignEngine.Evaluation;

/// <summary>
/// Provides caching for compiled expressions to improve performance.
/// </summary>
internal class ExpressionCache
{
    private readonly ConcurrentDictionary<string, Func<object?, object?>> _lambdaCache = new();

    /// <summary>
    /// Tries to get a cached lambda for the given key.
    /// </summary>
    public bool TryGetLambda(string key, out Func<object?, object?> lambda)
    {
        return _lambdaCache.TryGetValue(key, out lambda);
    }

    /// <summary>
    /// Caches a lambda for future use.
    /// </summary>
    public void CacheLambda(string key, Func<object?, object?> lambda)
    {
        // Use GetOrAdd to handle race conditions
        _lambdaCache.AddOrUpdate(key, lambda, (_, _) => lambda);
    }

    /// <summary>
    /// Clears the expression cache.
    /// </summary>
    public void Clear()
    {
        _lambdaCache.Clear();
    }
}