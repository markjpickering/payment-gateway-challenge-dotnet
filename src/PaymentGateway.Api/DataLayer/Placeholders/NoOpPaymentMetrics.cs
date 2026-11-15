namespace PaymentGateway.DataLayer.Placeholders;

/// <summary>
/// PLACEHOLDER for <see cref="IPaymentMetrics"/> (ADR-15): it records nothing. A real one would be a
/// <c>System.Diagnostics.Metrics</c> meter exported through OpenTelemetry. It is kept as a no-op so the bank client's call
/// site shows where metrics are recorded, without the extra packages.
/// </summary>
public sealed class NoOpPaymentMetrics : IPaymentMetrics
{
    public void PaymentProcessed(PaymentStatus status)
    {
    }

    public void BankRequest(BankOutcome outcome, TimeSpan duration)
    {
    }
}