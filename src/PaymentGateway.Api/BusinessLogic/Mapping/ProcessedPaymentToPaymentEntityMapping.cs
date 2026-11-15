namespace PaymentGateway.BusinessLogic.Mapping;

/// <summary>
/// Maps a processed payment to the entity that is stored (ADR-14, ADR-26). Only the last four card digits are kept: the
/// full card number and the CVV never leave the request. The request must already have passed validation, so the
/// numeric fields are known to be present.
/// </summary>
public sealed class ProcessedPaymentToPaymentEntityMapping : IMapping<ProcessedPaymentModel, PaymentEntity>
{
    public PaymentEntity Map(ProcessedPaymentModel source) => new(
        source.Id,
        source.MerchantId,
        source.Status,
        source.Request.CardNumber[^4..],
        source.Request.ExpiryMonth!.Value,
        source.Request.ExpiryYear!.Value,
        source.Request.Currency,
        source.Request.Amount!.Value);
}