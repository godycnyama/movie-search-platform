using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Settings;

public class PostgresSettings
{
    [Required]
    public string PostgresConnectionString { get; set; } = string.Empty;
}
