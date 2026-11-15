namespace PaymentGateway.Api.Security;

/// <summary>The OAuth scopes a caller needs (ADR-7). Each is also the name of the policy that requires it.</summary>
public static class PaymentScopes
{
    public const string Create = "payments:create";
    public const string Read = "payments:read";
}