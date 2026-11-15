namespace PaymentGateway.BusinessLogic.Mapping;

/// <summary>
/// Maps a merchant's payment request to the bank's request (ADR-14). The request must already have
/// passed validation, so the numeric fields are known to be present.
/// </summary>
public sealed class PostPaymentRequestToBankPaymentRequestMapping : IMapping<PostPaymentRequest, BankPaymentRequest>
{
    public BankPaymentRequest Map(PostPaymentRequest source) => new()
    {
        CardNumber = source.CardNumber,
        ExpiryDate = $"{source.ExpiryMonth!.Value:D2}/{source.ExpiryYear!.Value:D4}",
        Currency = source.Currency,
        Amount = source.Amount!.Value,
        Cvv = source.Cvv
    };
}