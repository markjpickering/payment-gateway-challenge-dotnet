namespace PaymentGateway.DataInterface;

/// <summary>
/// The business metrics the gateway reports (ADR-15). Synchronous on purpose: recording a measurement is in-memory
/// computation, not I/O (ADR-16). No method takes card data, payment ids or merchant ids: tags are fixed, low-cardinality values.
/// <para>On the <c>lean</c> branch only a PLACEHOLDER implementation exists (<see cref="NoOpPaymentMetrics"/>).</para>
/// </summary>
public interface IPaymentMetrics
{
    /// <summary>A payment request reached an outcome: <c>Authorized</c>, <c>Declined</c> or <c>Rejected</c>.</summary>
    void PaymentProcessed(PaymentStatus status);

    /// <summary>A call to the bank ended, and how long it took.</summary>
    void BankRequest(BankOutcome outcome, TimeSpan duration);
}