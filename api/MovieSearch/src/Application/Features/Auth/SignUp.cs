using Application.Common;
using Application.Repositories;
using Application.Requests;
using Application.Responses;
using Application.Services;
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
/// Creates an account and returns a bearer token (<c>POST /api/v1/auth/signup</c>).
/// The very first account is bootstrapped as "admin"; every account after that is "reader"
/// (further admins are promoted via <c>POST /api/v1/auth/assignadminrole</c>).
/// </summary>
public sealed record SignUpCommand(string Email, string Password);

public static class SignUpHandler
{
    public static async Task<Result<TokenResponse>> Handle(
        SignUpCommand command,
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        ILogger<SignUpCommand> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var email = command.Email.Trim().ToLowerInvariant();

            if (await userRepository.EmailExistsAsync(email, cancellationToken))
            {
                return Result<TokenResponse>.Failure(UserErrors.EmailTaken(email));
            }

            // Day-zero bootstrap: the first account ever created becomes the admin.
            var isFirstUser = !await userRepository.AnyAsync(cancellationToken);

            var now = DateTimeOffset.UtcNow;
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = passwordHasher.Hash(command.Password),
                Role = isFirstUser ? UserRoles.Admin : UserRoles.Reader,
                CreatedAt = now,
                UpdatedAt = now,
            };

            await userRepository.AddAsync(user, cancellationToken);

            logger.LogInformation("User {UserId} signed up with role {Role}", user.Id, user.Role);
            return Result<TokenResponse>.Success(tokenService.GenerateToken(user));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Sign-up failed");
            throw;
        }
    }
}

public sealed class SignUpEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapApiGroup("Auth").MapPost("/auth/signup", async (
                SignUpRequest request,
                IMessageBus bus,
                CancellationToken cancellationToken) =>
            {
                if (RequestValidation.HasErrors(request, out var errors))
                {
                    return Results.ValidationProblem(errors);
                }

                var result = await bus.InvokeAsync<Result<TokenResponse>>(
                    new SignUpCommand(request.Email, request.Password), cancellationToken);

                return result.IsSuccess
                    ? Results.Created((string?)null, result.Value)
                    : result.Error!.ToProblem(StatusCodes.Status409Conflict);
            })
           .AllowAnonymous()
           .WithName("SignUp")
           .WithTags("Auth")
           .Produces<TokenResponse>(StatusCodes.Status201Created)
           .ProducesValidationProblem()
           .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
