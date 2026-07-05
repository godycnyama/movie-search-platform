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
            return Result<MessageResponse>.Failure(Error.Unexpected);
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

                if (result.IsSuccess)
                {
                    return Results.Ok(result.Value);
                }

                var statusCode = result.Error! == Error.Unexpected
                    ? StatusCodes.Status500InternalServerError
                    : StatusCodes.Status404NotFound;

                return result.Error!.ToProblem(statusCode);
            })
           .RequireAuthorization(AuthPolicies.AdminOnly)
           .WithName("AssignAdminRole")
           .WithTags("Auth")
           .Produces<MessageResponse>()
           .ProducesValidationProblem()
           .ProducesProblem(StatusCodes.Status401Unauthorized)
           .ProducesProblem(StatusCodes.Status403Forbidden)
           .ProducesProblem(StatusCodes.Status404NotFound)
           .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
