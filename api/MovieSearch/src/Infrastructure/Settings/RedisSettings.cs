using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Settings;

/// <summary>
/// Redis cache settings (StackExchange.Redis conventions). The connection string may
/// carry credentials ("host:port,password=..."), so production values must come from
/// environment variables or user secrets — only credential-free defaults belong in
/// committed configuration.
/// </summary>
public class RedisSettings
{
    /// <summary>StackExchange.Redis configuration string, e.g. "localhost:6379" or "redis:6379,password=...".</summary>
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Prefix applied to every cache key, so multiple apps can share one Redis.</summary>
    public string InstanceName { get; set; } = "movie-search:";

    /// <summary>Logical Redis database index.</summary>
    [Range(0, 15)]
    public int Database { get; set; }

    /// <summary>Time allowed to establish a connection before failing.</summary>
    [Range(100, 60_000)]
    public int ConnectTimeoutMs { get; set; } = 5_000;

    /// <summary>Time allowed for synchronous operations before failing.</summary>
    [Range(100, 60_000)]
    public int SyncTimeoutMs { get; set; } = 5_000;

    /// <summary>Connection attempts before giving up during the initial connect.</summary>
    [Range(1, 10)]
    public int ConnectRetry { get; set; } = 3;

    /// <summary>
    /// When false (recommended for containers), the client keeps retrying in the
    /// background instead of failing hard if Redis is unavailable at startup.
    /// </summary>
    public bool AbortOnConnectFail { get; set; }

    /// <summary>Encrypt the connection (required by most managed Redis offerings).</summary>
    public bool UseSsl { get; set; }

    /// <summary>Default time-to-live for cached entries when the caller does not specify one.</summary>
    [Range(1, 86_400)]
    public int DefaultTtlSeconds { get; set; } = 300;
}
