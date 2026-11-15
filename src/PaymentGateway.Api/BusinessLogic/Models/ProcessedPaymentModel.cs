namespace PaymentGateway.BusinessLogic.Models;

/// <summary>
/// A payment the bank has answered: the new id, the merchant it belongs to, the outcome, and the original request. It is
/// the source of the mapping to the stored entity, which keeps only the last four card digits (ADR-14). It is never stored.
/// </summary>
public record ProcessedPaymentModel(Guid Id, string MerchantId, PaymentStatus Status, PostPaymentRequest Request);