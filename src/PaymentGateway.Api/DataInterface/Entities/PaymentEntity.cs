namespace PaymentGateway.DataInterface.Entities;

/// <summary>
/// What the payments store keeps (ADR-26): the storage entity, deliberately separate from the request and response
/// DTOs so each can change without the other. It holds the owning merchant and the last four card digits only; the full
/// card number and the CVV are never stored.
/// </summary>
public sealed record PaymentEntity(
    Guid Id,
    string MerchantId,
    PaymentStatus Status,
    string CardNumberLastFour,
    int ExpiryMonth,
    int ExpiryYear,
    string Currency,
    int Amount);