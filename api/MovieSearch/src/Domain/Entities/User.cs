namespace Domain.Entities;

/// <summary>
/// An API user account. Authentication is email + password (PBKDF2 hash — the clear
/// text is never stored); authorization is role-based via <see cref="Role"/>
/// (see <c>UserRoles</c>: "reader" by default, "admin" for the stats endpoint).
/// </summary>
public class User
{
    /// <summary>Stable unique identifier, carried as the <c>sub</c> claim in JWTs.</summary>
    public Guid Id { get; set; }

    /// <summary>Login identifier; stored lower-cased and unique.</summary>
    public required string Email { get; set; }

    /// <summary>Salted PBKDF2 hash of the password, including its parameters.</summary>
    public required string PasswordHash { get; set; }

    public string Role { get; set; } = UserRoles.Reader;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
