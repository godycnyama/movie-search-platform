using Application.Features.Auth;
using Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace MovieSearch.Tests.UnitTests.Handlers.Auth;

/// <summary>
/// Covers <see cref="AssignAdminRoleHandler"/>: promoting a reader, the idempotent
/// already-admin case (no write), and the unknown-email failure.
/// </summary>
public class AssignAdminRoleHandlerTests
{
    [Fact]
    public async Task Handle_PromotesAReaderToAdmin()
    {
        var user = Fakes.SampleUser("john@example.com", role: UserRoles.Reader);
        var users = new FakeUserRepository(user);

        var result = await AssignAdminRoleHandler.Handle(
            new AssignAdminRoleCommand("JOHN@example.com"), // matched case-insensitively
            users, NullLogger<AssignAdminRoleCommand>.Instance, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Message.ShouldContain("assigned");
        user.Role.ShouldBe(UserRoles.Admin);
        users.UpdateCalls.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_IsIdempotent_WhenTheUserIsAlreadyAdmin()
    {
        var user = Fakes.SampleUser("john@example.com", role: UserRoles.Admin);
        var users = new FakeUserRepository(user);

        var result = await AssignAdminRoleHandler.Handle(
            new AssignAdminRoleCommand("john@example.com"),
            users, NullLogger<AssignAdminRoleCommand>.Instance, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Message.ShouldContain("already");
        users.UpdateCalls.ShouldBe(0); // no write for a no-op
    }

    [Fact]
    public async Task Handle_FailsWhenTheEmailIsUnknown()
    {
        var users = new FakeUserRepository();

        var result = await AssignAdminRoleHandler.Handle(
            new AssignAdminRoleCommand("nobody@example.com"),
            users, NullLogger<AssignAdminRoleCommand>.Instance, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error!.Code.ShouldBe("User.NotFound");
    }
}
