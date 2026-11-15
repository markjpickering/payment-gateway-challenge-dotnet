using System.Text.Json.Serialization;

namespace PaymentGateway.DataInterface.Dtos.Requests;

/// <summary>The request the bank simulator expects (snake_case JSON, expiry as zero-padded MM/YYYY).</summary>
public record BankPaymentRequest
{
    [JsonPropertyName("card_number")]
    public required string CardNumber { get; init; }

    [JsonPropertyName("expiry_date")]
    public required string ExpiryDate { get; init; }

    [JsonPropertyName("currency")]
    public required string Currency { get; init; }

    [JsonPropertyName("amount")]
    public required int Amount { get; init; }

    [JsonPropertyName("cvv")]
    public required string Cvv { get; init; }

    // A record's generated ToString() prints every property; redact the card number and CVV (ADR-9).
    public override string ToString() =>
        $"BankPaymentRequest {{ CardNumber = [redacted], ExpiryDate = {ExpiryDate}, Currency = {Currency}, " +
        $"Amount = {Amount}, Cvv = [redacted] }}";
}