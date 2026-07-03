using Domain.Errors;

namespace Domain.Common;

/// <summary>
/// Outcome of an operation that can fail for a domain reason: either a value or an
/// <see cref="Errors.Error"/>, never both. Lets handlers report *why* something failed
/// without exceptions, so endpoints can map each error code to the right status.
/// </summary>
public sealed record Result<T>
{
    public T? Value { get; }

    public Error? Error { get; }

    public bool IsSuccess => Error is null;

    private Result(T? value, Error? error)
    {
        Value = value;
        Error = error;
    }

    public static Result<T> Success(T value) => new(value, null);

    public static Result<T> Failure(Error error) => new(default, error);
}
