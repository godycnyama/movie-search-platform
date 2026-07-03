using System.Security.Claims;
using Application.Common;
using Application.Repositories;
using Application.Requests;
using Application.Responses;
using Application.Services;
using Carter;
using Domain.Common;
using Domain.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Application.Features.Auth;

/// <summary>
/// Changes the authenticated user's password (<c>POST /api/v1/auth/change-password</c>).
/// The user id comes from the bearer token's <c>sub</c> claim.
/// </summary>
public sealed record ChangePasswordCommand(Guid UserId, string CurrentPassword, string NewPassword);

public static class ChangePasswordHandler
{
    public static async Task<Result<MessageResponse>> Handle(
        ChangePasswordCommand command,
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ILogger<ChangePasswordCommand> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await userRepository.GetByIdAsync(command.UserId, cancellationToken);
            if (user is null)
            {
                return Result<MessageResponse>.Failure(UserErrors.NotFound(command.UserId));
            }

            if (!passwordHasher.Verify(command.CurrentPassword, user.PasswordHash))
            {
                return Result<MessageResponse>.Failure(UserErrors.PasswordIncorrect());
            }

            user.PasswordHash = passwordHasher.Hash(command.NewPassword);
            user.UpdatedAt = DateTimeOffset.UtcNow;
            await userRepository.UpdateAsync(user, cancellationToken);

            logger.LogInformation("User {UserId} changed their password", user.Id);
            return Result<MessageResponse>.Success(new MessageResponse("Password changed successfully."));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Password change failed for user {UserId}", command.UserId);
            throw;
        }
    }
}

public sealed class ChangePasswordEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapApiGroup("Auth").MapPost("/auth/change-password", async (
                ChangePasswordRequest request,
                ClaimsPrincipal principal,
                IMessageBus bus,
                CancellationToken cancellationToken) =>
            {
                if (RequestValidation.HasErrors(request, out var errors))
                {
                    return Results.ValidationProblem(errors);
                }

                // "sub" is the raw JWT subject claim (inbound claim mapping is disabled).
                if (!Guid.TryParse(principal.FindFirstValue("sub"), out var userId))
                {
                    return Results.Unauthorized();
                }

                var result = await bus.InvokeAsync<Result<MessageResponse>>(
                    new ChangePasswordCommand(userId, request.CurrentPassword, request.NewPassword),
                    cancellationToken);

                if (result.IsSuccess)
                {
                    return Results.Ok(result.Value);
                }

                return result.Error!.Code == UserErrors.PasswordIncorrect().Code
                    ? result.Error.ToProblem(StatusCodes.Status400BadRequest)
                    : result.Error.ToProblem(StatusCodes.Status404NotFound);
            })
           .RequireAuthorization()
           .WithName("ChangePassword")
           .WithTags("Auth")
           .Produces<MessageResponse>()
           .ProducesValidationProblem()
           .ProducesProblem(StatusCodes.Status400BadRequest)
           .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
