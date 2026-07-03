using System.Text.Json;
using Application.Services;
using Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Infrastructure.Services;

/// <summary>
/// Redis-backed <see cref="ICacheService"/> (StackExchange.Redis). Configuration comes
/// from <see cref="IOptionsMonitor{RedisSettings}"/>: key prefix, TTL and database index
/// are re-read on every call, and a connection-settings change swaps the multiplexer so
/// new values apply without a restart. The connection is lazy (nothing dials Redis at
/// startup) and every operation degrades to a miss/no-op on failure — a cache outage
/// must never fail a request.
/// </summary>
public sealed class CacheService : ICacheService, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = JsonSerializerOptions.Web;

    private readonly IOptionsMonitor<RedisSettings> _settingsMonitor;
    private readonly ILogger<CacheService> _logger;
    private readonly IDisposable? _reloadSubscription;
    private Lazy<IConnectionMultiplexer> _connection;

    public CacheService(IOptionsMonitor<RedisSettings> settingsMonitor, ILogger<CacheService> logger)
    {
        _settingsMonitor = settingsMonitor;
        _logger = logger;
        _connection = CreateLazyConnection(settingsMonitor.CurrentValue);

        _reloadSubscription = settingsMonitor.OnChange(settings =>
        {
            var previous = Interlocked.Exchange(ref _connection, CreateLazyConnection(settings));
            if (previous.IsValueCreated)
            {
                previous.Value.Dispose();
            }

            _logger.LogInformation("Redis settings changed; the connection will be re-established on next use");
        });
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await Database.StringGetAsync(PrefixedKey(key));
            return value.HasValue ? JsonSerializer.Deserialize<T>((string)value!, SerializerOptions) : default;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Redis GET failed for key '{Key}'; treating as a cache miss", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? timeToLive = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = JsonSerializer.Serialize(value, SerializerOptions);
            await Database.StringSetAsync(PrefixedKey(key), payload, timeToLive ?? DefaultTtl);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Redis SET failed for key '{Key}'; value not cached", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await Database.KeyDeleteAsync(PrefixedKey(key));
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Redis DEL failed for key '{Key}'", key);
        }
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? timeToLive = null,
        CancellationToken cancellationToken = default)
    {
        var cached = await GetAsync<T>(key, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var created = await factory(cancellationToken);
        if (created is not null)
        {
            await SetAsync(key, created, timeToLive, cancellationToken);
        }

        return created;
    }

    public void Dispose()
    {
        _reloadSubscription?.Dispose();
        if (_connection.IsValueCreated)
        {
            _connection.Value.Dispose();
        }
    }

    private IDatabase Database => _connection.Value.GetDatabase(_settingsMonitor.CurrentValue.Database);

    private TimeSpan DefaultTtl => TimeSpan.FromSeconds(_settingsMonitor.CurrentValue.DefaultTtlSeconds);

    private string PrefixedKey(string key) => $"{_settingsMonitor.CurrentValue.InstanceName}{key}";

    private static Lazy<IConnectionMultiplexer> CreateLazyConnection(RedisSettings settings) =>
        new(() => ConnectionMultiplexer.Connect(BuildConfiguration(settings)),
            LazyThreadSafetyMode.ExecutionAndPublication);

    private static ConfigurationOptions BuildConfiguration(RedisSettings settings)
    {
        var configuration = ConfigurationOptions.Parse(settings.ConnectionString);
        configuration.ConnectTimeout = settings.ConnectTimeoutMs;
        configuration.SyncTimeout = settings.SyncTimeoutMs;
        configuration.ConnectRetry = settings.ConnectRetry;
        configuration.AbortOnConnectFail = settings.AbortOnConnectFail;
        configuration.Ssl = settings.UseSsl;
        configuration.DefaultDatabase = settings.Database;
        return configuration;
    }
}
