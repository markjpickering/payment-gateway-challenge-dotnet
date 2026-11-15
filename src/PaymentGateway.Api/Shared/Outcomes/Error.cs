namespace PaymentGateway.Shared.Outcomes;

/// <summary>
/// An expected failure. The message must be safe to show to a caller: it never contains card data (ADR-17).
/// <see cref="FieldErrors"/> is filled for <see cref="ErrorKind.ValidationFailed"/> and empty otherwise.
/// </summary>
public record Error(ErrorKind Kind, string Message)
{
    public IReadOnlyList<FieldError> FieldErrors { get; init; } = [];
}