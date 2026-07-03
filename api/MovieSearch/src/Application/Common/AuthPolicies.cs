using Domain.Entities;

namespace Application.Common;

/// <summary>Named authorization policies, defined once so endpoints and registration can't drift.</summary>
public static class AuthPolicies
{
    /// <summary>Requires the <see cref="UserRoles.Admin"/> role (README §9: stats is admin-only).</summary>
    public const string AdminOnly = "AdminOnly";
}
