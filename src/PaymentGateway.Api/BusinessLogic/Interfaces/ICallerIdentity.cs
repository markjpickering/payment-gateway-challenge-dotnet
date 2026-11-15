namespace PaymentGateway.BusinessLogic.Interfaces;

/// <summary>
/// Who is making the current request, taken only from the validated token (ADR-7). The two identities are equal
/// when a merchant acts for itself and differ when someone acts for a merchant (a support user, a platform).
/// </summary>
public interface ICallerIdentity
{
    /// <summary>The principal who made the call (the <c>sub</c> claim).</summary>
    string InitiatedBy { get; }

    /// <summary>The merchant account the operation is for: <c>merchant_id</c>, else the client id, else the subject.</summary>
    string OnBehalfOf { get; }
}