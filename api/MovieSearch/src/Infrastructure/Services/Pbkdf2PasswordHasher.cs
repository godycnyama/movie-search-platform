using System.Security.Cryptography;
using Application.Services;

namespace Infrastructure.Services;

/// <summary>
/// PBKDF2-HMAC-SHA256 password hasher (600k iterations per OWASP guidance).
/// Hash format: <c>pbkdf2-sha256.{iterations}.{salt-b64}.{hash-b64}</c> — parameters
/// travel with the hash, so they can be raised later without invalidating old hashes.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const string Algorithm = "pbkdf2-sha256";
    private const int Iterations = 600_000;
    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSizeBytes);
        return $"{Algorithm}.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string passwordHash)
    {
        var parts = passwordHash.Split('.');
        if (parts is not [Algorithm, var iterationsPart, var saltPart, var hashPart]
            || !int.TryParse(iterationsPart, out var iterations))
        {
            return false;
        }

        var salt = Convert.FromBase64String(saltPart);
        var expected = Convert.FromBase64String(hashPart);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
