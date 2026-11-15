namespace PaymentGateway.BusinessLogic.Interfaces;

/// <summary>
/// Processes a payment: validate, call the bank, store. A request that is not valid or a bank that
/// cannot be used is a failed <see cref="Result{T}"/>; a bank that authorizes or declines is a success (ADR-17).
/// </summary>
public interface IPaymentService
{
    Task<Result<PostPaymentResponse>> ProcessAsync(PostPaymentRequest request, CancellationToken cancellationToken);

    /// <summary>The caller's merchant's payment, or null when it does not exist or belongs to another merchant.</summary>
    Task<PostPaymentResponse?> GetAsync(Guid id, CancellationToken cancellationToken);
}