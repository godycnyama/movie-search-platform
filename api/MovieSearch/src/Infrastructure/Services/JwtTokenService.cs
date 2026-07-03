using System.Text;
using Application.Responses;
using Application.Services;
using Domain.Entities;
using Infrastructure.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Services;

/// <summary>
/// Issues HMAC-SHA256 signed JWTs from <see cref="JwtSettings"/>. Claims use the raw
/// JWT names ("sub", "email", "role") — inbound claim mapping is disabled on the
/// bearer middleware to match.
/// </summary>
public sealed class JwtTokenService(IOptions<JwtSettings> jwtOptions) : ITokenService
{
    private static readonly JsonWebTokenHandler TokenHandler = new();

    public TokenResponse GenerateToken(User user)
    {
        var settings = jwtOptions.Value;
        var lifetime = TimeSpan.FromMinutes(settings.ExpiryMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            Expires = DateTime.UtcNow.Add(lifetime),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
                SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [JwtRegisteredClaimNames.Email] = user.Email,
                ["role"] = user.Role,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
            },
        };

        return new TokenResponse
        {
            AccessToken = TokenHandler.CreateToken(descriptor),
            TokenType = "Bearer",
            ExpiresIn = (int)lifetime.TotalSeconds,
            Role = user.Role,
        };
    }
}
