using Application.Features.Auth;
using Microsoft.Extensions.Logging.Abstractions;

namespace MovieSearch.Tests.UnitTests.Handlers.Auth;

/// <summary>
/// Covers <see cref="ChangePasswordHandler"/>: the not-found and wrong-current-password
/// failures, and the happy path that re-hashes and persists the new password.
/// </summary>
public class ChangePasswordHandlerTests
{
    private readonly FakePasswordHasher _hasher = new();

    [Fact]
    public async Task Handle_ChangesThePassword_WhenTheCurrentOneIsCorrect()
    {
        var user = Fakes.SampleUser(passwordHash: "hash:OldPassword123!");
        var users = new FakeUserRepository(user);

        var result = await ChangePasswordHandler.Handle(
            new ChangePasswordCommand(user.Id, "OldPassword123!", "NewPassword123!"),
            users, _hasher, NullLogger<ChangePasswordCommand>.Instance, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Message.ShouldBe("Password changed successfully.");
        user.PasswordHash.ShouldBe("hash:NewPassword123!");
        users.UpdateCalls.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_FailsWhenTheUserDoesNotExist()
    {
        var users = new FakeUserRepository(); // no accounts

        var result = await ChangePasswordHandler.Handle(
            new ChangePasswordCommand(Guid.NewGuid(), "OldPassword123!", "NewPassword123!"),
            users, _hasher, NullLogger<ChangePasswordCommand>.Instance, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error!.Code.ShouldBe("User.NotFound");
        users.UpdateCalls.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_FailsWhenTheCurrentPasswordIsWrong()
    {
        var user = Fakes.SampleUser(passwordHash: "hash:OldPassword123!");
        var users = new FakeUserRepository(user);

        var result = await ChangePasswordHandler.Handle(
            new ChangePasswordCommand(user.Id, "NotMyPassword!", "NewPassword123!"),
            users, _hasher, NullLogger<ChangePasswordCommand>.Instance, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error!.Code.ShouldBe("User.PasswordIncorrect");
        user.PasswordHash.ShouldBe("hash:OldPassword123!"); // unchanged
        users.UpdateCalls.ShouldBe(0);
    }
}
