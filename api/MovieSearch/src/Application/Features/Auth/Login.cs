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

public sealed record LoginCommand(string Email, string Password);

public static class LoginHandler
{
    public static async Task<Result<TokenResponse>> Handle(
        LoginCommand command,
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        ILogger<LoginCommand> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await userRepository.GetByEmailAsync(command.Email.Trim().ToLowerInvariant(), cancellationToken);

            // Same error for unknown email and wrong password — don't leak which accounts exist.
            if (user is null || !passwordHasher.Verify(command.Password, user.PasswordHash))
            {
                return Result<TokenResponse>.Failure(UserErrors.InvalidCredentials());
            }

            return Result<TokenResponse>.Success(tokenService.GenerateToken(user));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Login failed");
            return Result<TokenResponse>.Failure(Error.Unexpected);
        }
    }
}

public sealed class LoginEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapApiGroup("Auth").MapPost("/auth/login", async (
                LoginRequest request,
                IMessageBus bus,
                CancellationToken cancellationToken) =>
            {
                if (RequestValidation.HasErrors(request, out var errors))
                {
                    return Results.ValidationProblem(errors);
                }

                var result = await bus.InvokeAsync<Result<TokenResponse>>(
                    new LoginCommand(request.Email, request.Password), cancellationToken);

                if (result.IsSuccess)
                {
                    return Results.Ok(result.Value);
                }

                var statusCode = result.Error! == Error.Unexpected
                    ? StatusCodes.Status500InternalServerError
                    : StatusCodes.Status401Unauthorized;

                return result.Error!.ToProblem(statusCode);
            })
           .AllowAnonymous()
           .WithName("Login")
           .WithTags("Auth")
           .Produces<TokenResponse>()
           .ProducesValidationProblem()
           .ProducesProblem(StatusCodes.Status401Unauthorized)
           .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
