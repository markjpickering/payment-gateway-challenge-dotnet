namespace PaymentGateway.BusinessLogic.Mapping;

/// <summary>Maps a stored payment to the response a merchant sees; the owning merchant is not part of the response (ADR-14, ADR-26).</summary>
public sealed class PaymentEntityToPostPaymentResponseMapping : IMapping<PaymentEntity, PostPaymentResponse>
{
    public PostPaymentResponse Map(PaymentEntity source) => new()
    {
        Id = source.Id,
        Status = source.Status,
        CardNumberLastFour = source.CardNumberLastFour,
        ExpiryMonth = source.ExpiryMonth,
        ExpiryYear = source.ExpiryYear,
        Currency = source.Currency,
        Amount = source.Amount
    };
}