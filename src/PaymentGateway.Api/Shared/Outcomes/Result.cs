using System.Diagnostics.CodeAnalysis;

namespace PaymentGateway.Shared.Outcomes;

/// <summary>
/// Result pattern: carries either a value or an <see cref="Error"/>, so expected failures are returned
/// rather than thrown (ADR-17). Hand-written on purpose; no third-party result library.
/// </summary>
public sealed record Result<T>
{
    private Result(T? value, Error? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public Error? Error { get; }

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static Result<T> Success(T value) => new(value, null);

    public static Result<T> Failure(Error error) => new(default, error);
}