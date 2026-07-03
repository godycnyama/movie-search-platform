namespace Domain.Entities;

/// <summary>
/// The roles this API grants (README §9/§10: tokens carry "reader" or "admin";
/// the stats endpoint requires "admin").
/// </summary>
public static class UserRoles
{
    public const string Reader = "reader";
    public const string Admin = "admin";
}
