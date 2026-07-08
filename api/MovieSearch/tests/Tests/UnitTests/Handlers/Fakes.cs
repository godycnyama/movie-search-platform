using Application.Repositories;
using Application.Responses;
using Application.Services;
using Domain.Common;
using Domain.Entities;

namespace MovieSearch.Tests.UnitTests.Handlers;

/// <summary>
/// Hand-rolled test doubles for the Wolverine handler unit tests. The solution has no
/// mocking library (see MovieSearch.Tests.csproj), so the ports are faked directly —
/// each fake counts calls and lets a test configure the result it returns (or throw,
/// to exercise a handler's catch block).
/// </summary>
internal static class Fakes
{
    public static readonly Guid SampleId = Guid.Parse("9ddc4d0d-acde-45b1-8f32-a22fd9134d71");

    public static MovieCatalogItem SampleMovie(Guid? id = null, string title = "Interstellar", double? similarity = 0.9612) =>
        new(id ?? SampleId, title, 2014, "Adventure", "Christopher Nolan", "Paramount Pictures",
            "PG-13", 8.7, 91, 165_000_000, 169, "high", 2010, similarity);

    public static User SampleUser(
        string email = "john@example.com",
        string passwordHash = "hash:MyPassword123!",
        string role = UserRoles.Reader) =>
        new()
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = passwordHash,
            Role = role,
            CreatedAt = DateTimeOffset.UnixEpoch,
            UpdatedAt = DateTimeOffset.UnixEpoch,
        };
}

/// <summary>Read port for the catalogue. Every method counts its calls and returns a configurable result.</summary>
internal sealed class FakeMovieCatalogService : IMovieCatalogService
{
    public int SearchCalls { get; private set; }
    public int GetByIdCalls { get; private set; }
    public int GetByTitleCalls { get; private set; }
    public int GetSimilarCalls { get; private set; }
    public int GetGenresCalls { get; private set; }
    public int GetStatisticsCalls { get; private set; }

    public string? LastQuery { get; private set; }
    public MovieSearchFilters? LastFilters { get; private set; }
    public int LastTopK { get; private set; }

    public Func<Result<IReadOnlyList<MovieCatalogItem>>> OnSearch { get; set; } =
        () => Result<IReadOnlyList<MovieCatalogItem>>.Success(new List<MovieCatalogItem>());
    public Func<Result<MovieCatalogItem>> OnGetById { get; set; } =
        () => Result<MovieCatalogItem>.Success(Fakes.SampleMovie());
    public Func<Result<MovieCatalogItem>> OnGetByTitle { get; set; } =
        () => Result<MovieCatalogItem>.Success(Fakes.SampleMovie());
    public Func<Result<IReadOnlyList<MovieCatalogItem>>> OnGetSimilar { get; set; } =
        () => Result<IReadOnlyList<MovieCatalogItem>>.Success(new List<MovieCatalogItem>());
    public Func<Result<IReadOnlyList<string>>> OnGetGenres { get; set; } =
        () => Result<IReadOnlyList<string>>.Success(new List<string>());
    public Func<Result<MovieStatistics>> OnGetStatistics { get; set; } =
        () => Result<MovieStatistics>.Success(new MovieStatistics(0, 0, 0, null, null, null, null));

    public Task<Result<IReadOnlyList<MovieCatalogItem>>> SearchAsync(
        string query, MovieSearchFilters filters, int topK, CancellationToken cancellationToken = default)
    {
        SearchCalls++;
        LastQuery = query;
        LastFilters = filters;
        LastTopK = topK;
        return Task.FromResult(OnSearch());
    }

    public Task<Result<MovieCatalogItem>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        GetByIdCalls++;
        return Task.FromResult(OnGetById());
    }

    public Task<Result<MovieCatalogItem>> GetByTitleAsync(string title, CancellationToken cancellationToken = default)
    {
        GetByTitleCalls++;
        return Task.FromResult(OnGetByTitle());
    }

    public Task<Result<IReadOnlyList<MovieCatalogItem>>> GetSimilarAsync(
        Guid id, int topK, CancellationToken cancellationToken = default)
    {
        GetSimilarCalls++;
        LastTopK = topK;
        return Task.FromResult(OnGetSimilar());
    }

    public Task<Result<IReadOnlyList<string>>> GetGenresAsync(CancellationToken cancellationToken = default)
    {
        GetGenresCalls++;
        return Task.FromResult(OnGetGenres());
    }

    public Task<Result<MovieStatistics>> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        GetStatisticsCalls++;
        return Task.FromResult(OnGetStatistics());
    }
}

/// <summary>In-memory cache. Reads hit the dictionary; writes store by key. Tracks hit/write counts.</summary>
internal sealed class FakeCacheService : ICacheService
{
    private readonly Dictionary<string, object> _store = new(StringComparer.Ordinal);

    public int Hits { get; private set; }
    public int Writes { get; private set; }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(key, out var value))
        {
            Hits++;
            return Task.FromResult((T?)value);
        }

        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? timeToLive = null, CancellationToken cancellationToken = default)
    {
        Writes++;
        _store[key] = value!;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _store.Remove(key);
        return Task.CompletedTask;
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key, Func<CancellationToken, Task<T>> factory, TimeSpan? timeToLive = null, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(key, out var value))
        {
            Hits++;
            return (T)value;
        }

        var created = await factory(cancellationToken);
        _store[key] = created!;
        Writes++;
        return created;
    }
}

/// <summary>In-memory user store seeded via the constructor; counts writes.</summary>
internal sealed class FakeUserRepository : IUserRepository
{
    private readonly List<User> _users;

    public FakeUserRepository(params User[] seed) => _users = seed.ToList();

    public int AddCalls { get; private set; }
    public int UpdateCalls { get; private set; }

    public User? Single => _users.SingleOrDefault();
    public IReadOnlyList<User> Users => _users;

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_users.FirstOrDefault(u => u.Id == id));

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => Task.FromResult(_users.FirstOrDefault(u => u.Email == email));

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
        => Task.FromResult(_users.Any(u => u.Email == email));

    public Task<bool> AnyAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_users.Count > 0);

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        AddCalls++;
        _users.Add(user);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        UpdateCalls++;
        return Task.CompletedTask;
    }
}

/// <summary>Deterministic hasher: the hash is simply <c>hash:&lt;password&gt;</c>, so verification is exact-match.</summary>
internal sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => $"hash:{password}";

    public bool Verify(string password, string passwordHash) => passwordHash == $"hash:{password}";
}

/// <summary>Records the user it was asked to tokenise and echoes their role back in the token.</summary>
internal sealed class FakeTokenService : ITokenService
{
    public int Calls { get; private set; }
    public User? LastUser { get; private set; }

    public TokenResponse GenerateToken(User user)
    {
        Calls++;
        LastUser = user;
        return new TokenResponse
        {
            AccessToken = "test-token",
            TokenType = "Bearer",
            ExpiresIn = 3600,
            Role = user.Role,
        };
    }
}
