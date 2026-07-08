using Application.Features.Auth;
using Microsoft.Extensions.Logging.Abstractions;

namespace MovieSearch.Tests.UnitTests.Handlers.Auth;

/// <summary>
/// Covers <see cref="LoginHandler"/>: a token on valid credentials, and the deliberately
/// identical <c>User.InvalidCredentials</c> failure for both an unknown email and a wrong
/// password (so responses never leak which accounts exist).
/// </summary>
public class LoginHandlerTests
{
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeTokenService _tokens = new();

    [Fact]
    public async Task Handle_ReturnsAToken_ForValidCredentials()
    {
        var users = new FakeUserRepository(Fakes.SampleUser("john@example.com", "hash:MyPassword123!"));

        var result = await LoginHandler.Handle(
            new LoginCommand("JOHN@example.com", "MyPassword123!"), // email matched case-insensitively
            users, _hasher, _tokens, NullLogger<LoginCommand>.Instance, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.AccessToken.ShouldBe("test-token");
        _tokens.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_FailsWithInvalidCredentials_ForAnUnknownEmail()
    {
        var users = new FakeUserRepository(); // no accounts

        var result = await LoginHandler.Handle(
            new LoginCommand("nobody@example.com", "MyPassword123!"),
            users, _hasher, _tokens, NullLogger<LoginCommand>.Instance, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error!.Code.ShouldBe("User.InvalidCredentials");
        _tokens.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_FailsWithTheSameError_ForAWrongPassword()
    {
        var users = new FakeUserRepository(Fakes.SampleUser("john@example.com", "hash:MyPassword123!"));

        var result = await LoginHandler.Handle(
            new LoginCommand("john@example.com", "WrongPassword!"),
            users, _hasher, _tokens, NullLogger<LoginCommand>.Instance, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error!.Code.ShouldBe("User.InvalidCredentials"); // identical to the unknown-email case
    }
}
