namespace PaymentGateway.Shared.Outcomes;

/// <summary>
/// The kinds of expected failure a <see cref="Result{T}"/> can carry. Expected outcomes are values,
/// not exceptions (ADR-17); more kinds are added when a caller needs to tell them apart.
/// </summary>
public enum ErrorKind
{
    /// <summary>The request is invalid, so no payment was created and the bank was not called.</summary>
    ValidationFailed,

    /// <summary>The acquiring bank was unreachable, timed out or returned an unusable response.</summary>
    BankError
}