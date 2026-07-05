using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Settings;

public class RedisSettings
{
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    public string InstanceName { get; set; } = "movie-search:";

    public bool AbortOnConnectFail { get; set; }

    public bool UseSsl { get; set; }

    [Required]
    public int DefaultTtlSeconds { get; set; } = 300;
}
