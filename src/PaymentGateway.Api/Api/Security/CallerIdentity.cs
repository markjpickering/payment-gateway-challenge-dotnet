using System.Security.Claims;

namespace PaymentGateway.Api.Security;

/// <summary>Reads the caller's identities from the current request's validated token (ADR-7).</summary>
public sealed class CallerIdentity(IHttpContextAccessor accessor) : ICallerIdentity
{
    private static readonly string[] MerchantClaims = ["merchant_id", "client_id", "azp"];

    private ClaimsPrincipal User => accessor.HttpContext?.User
        ?? throw new InvalidOperationException("There is no current request to take the caller from.");

    public string InitiatedBy => User.FindFirstValue("sub")
        ?? throw new InvalidOperationException("The token has no subject claim.");

    public string OnBehalfOf => Describe(User).OnBehalfOf is { Length: > 0 } merchant ? merchant : InitiatedBy;

    /// <summary>
    /// Both identities from a token, with empty strings for whatever is missing. Does not throw, so a refused request
    /// can still be audited.
    /// </summary>
    public static (string InitiatedBy, string OnBehalfOf) Describe(ClaimsPrincipal user)
    {
        var subject = user.FindFirstValue("sub") ?? string.Empty;
        var merchant = MerchantClaims.Select(user.FindFirstValue).FirstOrDefault(value => value is not null) ?? subject;

        return (subject, merchant);
    }
}