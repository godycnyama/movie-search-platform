namespace Application.Services;

/// <summary>
/// One-way password hashing port. Implementations must use a salted, slow KDF
/// (PBKDF2 or better) and embed all parameters in the produced hash string so
/// they can evolve without invalidating stored hashes.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}
