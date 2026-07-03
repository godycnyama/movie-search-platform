using System.ComponentModel.DataAnnotations;

namespace Application.Common;

/// <summary>
/// Runs the DataAnnotations declared on the request DTOs (Application/Requests) and
/// shapes failures for <c>Results.ValidationProblem</c>, so endpoints return RFC 9457
/// validation problem details with the field-level messages defined on the contracts.
/// </summary>
internal static class RequestValidation
{
    public static bool HasErrors(object request, out Dictionary<string, string[]> errors)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);

        errors = results
            .SelectMany(r => r.MemberNames.DefaultIfEmpty(string.Empty), (r, member) => (Member: member, r.ErrorMessage))
            .GroupBy(x => x.Member)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ErrorMessage ?? "Invalid value.").ToArray());

        return errors.Count > 0;
    }
}
