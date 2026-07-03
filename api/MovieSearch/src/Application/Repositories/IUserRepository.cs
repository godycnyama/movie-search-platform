using Domain.Entities;

namespace Application.Repositories;

/// <summary>
/// Persistence port for user accounts. Unlike the read-only movie repository, the API
/// owns user writes, so mutating methods persist immediately.
/// Emails are matched case-insensitively (stored lower-cased).
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Inserts and persists a new user.</summary>
    Task AddAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>Persists changes to an existing (tracked or detached) user.</summary>
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);
}
