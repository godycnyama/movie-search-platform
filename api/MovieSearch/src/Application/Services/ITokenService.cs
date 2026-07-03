using Application.Responses;
using Domain.Entities;

namespace Application.Services;

/// <summary>Issues signed bearer tokens for authenticated users.</summary>
public interface ITokenService
{
    /// <summary>Creates a JWT carrying the user's id (<c>sub</c>), email and role claims.</summary>
    TokenResponse GenerateToken(User user);
}
