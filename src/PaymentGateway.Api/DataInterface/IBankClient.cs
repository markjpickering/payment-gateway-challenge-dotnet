namespace PaymentGateway.DataInterface;

/// <summary>
/// The acquiring bank. A bank that authorizes or declines is a successful <see cref="Result{T}"/>;
/// an unreachable or misbehaving bank is a failure with <see cref="ErrorKind.BankError"/> (ADR-9).
/// </summary>
public interface IBankClient
{
    Task<Result<BankPaymentResponse>> AuthorizeAsync(BankPaymentRequest request, CancellationToken cancellationToken);
}