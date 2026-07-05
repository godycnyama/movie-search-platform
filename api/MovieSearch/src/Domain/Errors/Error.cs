namespace Domain.Errors;

public sealed record Error(string Code, string Description)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    /// <summary>An unexpected/unhandled failure — maps to 500 rather than a domain status.</summary>
    public static readonly Error Unexpected = new("Error.Unexpected", "An unexpected error occurred.");
}
