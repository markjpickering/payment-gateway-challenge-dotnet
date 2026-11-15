using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using Microsoft.IdentityModel.Tokens;

namespace PaymentGateway.Api.Tests.TestSupport;

/// <summary>
/// Makes real signed JWTs for the integration tests. The application validates them exactly as it would a
/// <c>dotnet user-jwts</c> token; only the signing key, issuer and audience are the test's own (ADR-7).
/// </summary>
public static class TestTokens
{
    public const string Issuer = "payment-gateway-tests";
    public const string Audience = "payment-gateway-api";
    /// <summary>The subject of the default token. With no merchant_id claim it is also the merchant.</summary>
    public const string DefaultSubject = "test-user";
    public const string Create = "payments:create";
    public const string Read = "payments:read";

    public static readonly SymmetricSecurityKey SigningKey =
        new(Encoding.UTF8.GetBytes("test-only-signing-key-that-is-long-enough-for-hs256"));

    public static string For(
        string subject = DefaultSubject, string? merchantId = null, string[]? scopes = null,
        SecurityKey? key = null, TimeSpan? lifetime = null)
    {
        var now = DateTime.UtcNow;
        var claims = new List<Claim> { new("sub", subject), new("scope", string.Join(' ', scopes ?? [Create, Read])) };
        if (merchantId is not null)
        {
            claims.Add(new Claim("merchant_id", merchantId));
        }

        var expires = now + (lifetime ?? TimeSpan.FromHours(1));
        var token = new JwtSecurityToken(
            Issuer, Audience, claims, notBefore: expires < now ? expires.AddHours(-1) : now, expires: expires,
            signingCredentials: new SigningCredentials(key ?? SigningKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}