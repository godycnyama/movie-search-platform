using Application.Responses;
using Domain.Entities;

namespace Application.Services;

public interface ITokenService
{
    TokenResponse GenerateToken(User user);
}
