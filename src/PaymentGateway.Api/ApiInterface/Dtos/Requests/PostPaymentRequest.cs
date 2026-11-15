namespace PaymentGateway.ApiInterface.Dtos.Requests;

public record PostPaymentRequest
{
    public string CardNumber { get; init; } = string.Empty;

    // Numeric fields are nullable so a missing field can be told apart from zero and rejected as "required".
    public int? ExpiryMonth { get; init; }
    public int? ExpiryYear { get; init; }
    public string Currency { get; init; } = string.Empty;
    public int? Amount { get; init; }
    public string Cvv { get; init; } = string.Empty;

    // A record's generated ToString() prints every property, which would write the card number
    // and CVV to any log that formats the request. Redact them (ADR-9).
    public override string ToString() =>
        $"PostPaymentRequest {{ CardNumber = [redacted], ExpiryMonth = {ExpiryMonth}, ExpiryYear = {ExpiryYear}, " +
        $"Currency = {Currency}, Amount = {Amount}, Cvv = [redacted] }}";
}