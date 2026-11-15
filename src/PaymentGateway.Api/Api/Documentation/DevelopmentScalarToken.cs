#if DEBUG
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using Scalar.AspNetCore;

namespace PaymentGateway.Api.Documentation;

/// <summary>
/// FOR EASE OF TESTING ONLY. In Development, and only in a Debug build, the Scalar page is loaded with a development token so
/// "Authorize" is already filled in. It is signed with the same key <c>dotnet user-jwts</c> already put in your user
/// secrets, so it is exactly the token <c>scripts/dev-token.ps1</c> makes; nothing new is trusted. The whole file is
/// excluded from a Release build (<c>#if DEBUG</c>), and the documentation is only mapped in Development, so a deployed
/// service never issues a token (ADR-7).
/// </summary>
public static class DevelopmentScalarToken
{
    private const string Scheme = "Bearer";
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(8);

    /// <summary>Pre-loads the Scalar page with a token, if a user-jwts signing key is configured.</summary>
    public static void Prefill(ScalarOptions options, IConfiguration configuration, TimeProvider clock)
    {
        var token = TryCreate(configuration, clock);
        if (token is not null)
        {
            options.AddHttpAuthentication(Scheme, scheme => scheme.Token = token);
            options.AddPreferredSecuritySchemes(Scheme);
        }
    }

    /// <summary>The development token, or null when no signing key has been created yet (run scripts/dev-token.ps1 once).</summary>
    public static string? TryCreate(IConfiguration configuration, TimeProvider clock)
    {
        var bearer = configuration.GetSection("Authentication:Schemes:Bearer");
        var key = bearer.GetSection("SigningKeys").GetChildren().FirstOrDefault(k => !string.IsNullOrEmpty(k["Value"]));
        if (key is null)
        {
            return null;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = key["Issuer"] ?? bearer["ValidIssuer"],
            Audience = bearer.GetSection("ValidAudiences").GetChildren().Select(a => a.Value).FirstOrDefault(),
            NotBefore = now,
            Expires = now + Lifetime,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = "dev-user",
                ["merchant_id"] = "merchant-1",
                ["scope"] = "payments:create payments:read"
            },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Convert.FromBase64String(key["Value"]!)), SecurityAlgorithms.HmacSha256)
        });
    }
}
#endif