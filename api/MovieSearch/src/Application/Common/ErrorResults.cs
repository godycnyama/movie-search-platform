using Domain.Errors;
using Microsoft.AspNetCore.Http;

namespace Application.Common;

internal static class ErrorResults
{
    public static IResult ToProblem(this Error error, int statusCode) =>
        Results.Problem(title: error.Code, detail: error.Description, statusCode: statusCode);
}
