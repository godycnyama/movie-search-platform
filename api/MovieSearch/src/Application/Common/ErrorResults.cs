using Domain.Errors;
using Microsoft.AspNetCore.Http;

namespace Application.Common;

/// <summary>Translates a domain <see cref="Error"/> into an RFC 9457 problem response.</summary>
internal static class ErrorResults
{
    public static IResult ToProblem(this Error error, int statusCode) =>
        Results.Problem(title: error.Code, detail: error.Description, statusCode: statusCode);
}
