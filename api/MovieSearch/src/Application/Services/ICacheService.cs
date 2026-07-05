namespace Application.Services;

/// <summary>
/// Distributed cache port. Implementations must treat the cache as an optimisation,
/// not a dependency: a cache outage should degrade to misses, never fail the request.
/// Values are serialized as JSON; keys are namespaced by the implementation.
/// </summary>
public interface ICacheService
{
    /// <summary>Fetches a cached value, or <c>default</c> on a miss (or cache outage).</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    Task SetAsync<T>(string key, T value, TimeSpan? timeToLive = null, CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the cached value when present; otherwise invokes <paramref name="factory"/>,
    /// caches its result, and returns it. On a cache outage the factory result is returned uncached.
    /// </summary>
    Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? timeToLive = null,
        CancellationToken cancellationToken = default);
}
