using Application.Features.Auth;
using Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace MovieSearch.Tests.UnitTests.Handlers.Auth;

/// <summary>
/// Covers <see cref="SignUpHandler"/>: first-account-becomes-admin bootstrap, the
/// reader default, email normalisation, and the duplicate-email conflict.
/// </summary>
public class SignUpHandlerTests
{
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeTokenService _tokens = new();

    [Fact]
    public async Task Handle_MakesTheFirstAccountAnAdmin()
    {
        var users = new FakeUserRepository(); // empty store

        var result = await SignUpHandler.Handle(
            new SignUpCommand("john@example.com", "MyPassword123!"),
            users, _hasher, _tokens, NullLogger<SignUpCommand>.Instance, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        users.AddCalls.ShouldBe(1);
        users.Single!.Role.ShouldBe(UserRoles.Admin);
        result.Value!.Role.ShouldBe(UserRoles.Admin);
        _tokens.LastUser.ShouldBe(users.Single);
    }

    [Fact]
    public async Task Handle_MakesSubsequentAccountsReaders()
    {
        var users = new FakeUserRepository(Fakes.SampleUser("admin@example.com", role: UserRoles.Admin));

        var result = await SignUpHandler.Handle(
            new SignUpCommand("second@example.com", "MyPassword123!"),
            users, _hasher, _tokens, NullLogger<SignUpCommand>.Instance, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Role.ShouldBe(UserRoles.Reader);
    }

    [Fact]
    public async Task Handle_NormalisesAndHashesBeforePersisting()
    {
        var users = new FakeUserRepository();

        await SignUpHandler.Handle(
            new SignUpCommand("  John@Example.COM ", "MyPassword123!"),
            users, _hasher, _tokens, NullLogger<SignUpCommand>.Instance, CancellationToken.None);

        var stored = users.Single!;
        stored.Email.ShouldBe("john@example.com");
        stored.PasswordHash.ShouldBe("hash:MyPassword123!"); // never the clear text
    }

    [Fact]
    public async Task Handle_FailsWhenTheEmailIsAlreadyRegistered()
    {
        var users = new FakeUserRepository(Fakes.SampleUser("john@example.com"));

        var result = await SignUpHandler.Handle(
            new SignUpCommand("JOHN@example.com", "MyPassword123!"),
            users, _hasher, _tokens, NullLogger<SignUpCommand>.Instance, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error!.Code.ShouldBe("User.EmailTaken");
        users.AddCalls.ShouldBe(0);
        _tokens.Calls.ShouldBe(0);
    }
}
