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

            // the first account ever created becomes the admin.
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
            return Result<TokenResponse>.Failure(Error.Unexpected);
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

                if (result.IsSuccess)
                {
                    return Results.Created((string?)null, result.Value);
                }

                var statusCode = result.Error! == Error.Unexpected
                    ? StatusCodes.Status500InternalServerError
                    : StatusCodes.Status409Conflict;

                return result.Error!.ToProblem(statusCode);
            })
           .AllowAnonymous()
           .WithName("SignUp")
           .WithTags("Auth")
           .Produces<TokenResponse>(StatusCodes.Status201Created)
           .ProducesValidationProblem()
           .ProducesProblem(StatusCodes.Status409Conflict)
           .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
