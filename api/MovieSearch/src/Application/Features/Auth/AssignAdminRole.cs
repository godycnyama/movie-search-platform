using Application.Common;
using Application.Repositories;
using Application.Requests;
using Application.Responses;
using Carter;
using Domain.Common;
using Domain.Entities;
using Domain.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Application.Features.Auth;

/// <summary>
/// Promotes an existing user to the <see cref="UserRoles.Admin"/> role
/// (<c>POST /api/v1/auth/assignadminrole</c>). Admin-only; idempotent for
/// users who already hold the role.
/// </summary>
public sealed record AssignAdminRoleCommand(string Email);

public static class AssignAdminRoleHandler
{
    public static async Task<Result<MessageResponse>> Handle(
        AssignAdminRoleCommand command,
        IUserRepository userRepository,
        ILogger<AssignAdminRoleCommand> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var email = command.Email.Trim().ToLowerInvariant();
            var user = await userRepository.GetByEmailAsync(email, cancellationToken);
            if (user is null)
            {
                return Result<MessageResponse>.Failure(UserErrors.NotFound(email));
            }

            if (user.Role == UserRoles.Admin)
            {
                return Result<MessageResponse>.Success(
                    new MessageResponse($"'{email}' already has the admin role."));
            }

            user.Role = UserRoles.Admin;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            await userRepository.UpdateAsync(user, cancellationToken);

            logger.LogInformation("User {UserId} was assigned the admin role", user.Id);
            return Result<MessageResponse>.Success(
                new MessageResponse($"Admin role assigned to '{email}'."));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Admin role assignment failed for {Email}", command.Email);
            throw;
        }
    }
}

public sealed class AssignAdminRoleEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapApiGroup("Auth").MapPost("/auth/assignadminrole", async (
                AssignAdminRoleRequest request,
                IMessageBus bus,
                CancellationToken cancellationToken) =>
            {
                if (RequestValidation.HasErrors(request, out var errors))
                {
                    return Results.ValidationProblem(errors);
                }

                var result = await bus.InvokeAsync<Result<MessageResponse>>(
                    new AssignAdminRoleCommand(request.Email),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : result.Error!.ToProblem(StatusCodes.Status404NotFound);
            })
           .RequireAuthorization(AuthPolicies.AdminOnly)
           .WithName("AssignAdminRole")
           .WithTags("Auth")
           .Produces<MessageResponse>()
           .ProducesValidationProblem()
           .ProducesProblem(StatusCodes.Status401Unauthorized)
           .ProducesProblem(StatusCodes.Status403Forbidden)
           .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
