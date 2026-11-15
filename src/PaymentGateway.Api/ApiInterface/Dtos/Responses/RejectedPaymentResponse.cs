namespace PaymentGateway.ApiInterface.Dtos.Responses;

/// <summary>
/// The answer to a request that could not become a payment (the brief's <c>Rejected</c>): no payment was
/// created, the bank was not called, and each error names a field and a rule but never the submitted value.
/// </summary>
public record RejectedPaymentResponse(IReadOnlyList<FieldError> Errors)
{
    public PaymentStatus Status => PaymentStatus.Rejected;
}