namespace PaymentGateway.DataInterface.Telemetry;

/// <summary>How a call to the acquiring bank ended. A fixed set, so the metric tag stays low-cardinality.</summary>
public enum BankOutcome
{
    Authorized,
    Declined,
    Error
}