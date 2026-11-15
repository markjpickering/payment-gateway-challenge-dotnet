#if DEBUG
using System.Text;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using PaymentGateway.Api.Documentation;

namespace PaymentGateway.Api.Tests.Unit.Api.Documentation;

public class DevelopmentScalarTokenTests
{
    private const string Key = "a-development-only-signing-key-of-32-bytes!";
    private const string Issuer = "dotnet-user-jwts";
    private const string Audience = "https://localhost:7092";

    private static readonly FakeTimeProvider Clock = new(new DateTimeOffset(2026, 11, 15, 10, 0, 0, TimeSpan.Zero));

    private static IConfiguration ConfigWith(string? key) =>
        new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Authentication:Schemes:Bearer:ValidIssuer"] = Issuer,
                ["Authentication:Schemes:Bearer:ValidAudiences:0"] = Audience,
                ["Authentication:Schemes:Bearer:SigningKeys:0:Issuer"] = Issuer,
                ["Authentication:Schemes:Bearer:SigningKeys:0:Value"] = key is null ? null : Convert.ToBase64String(Encoding.UTF8.GetBytes(key)),
            }).Build();

    [Fact]
    public async Task TryCreate_WithASigningKey_MakesATokenThePaymentEndpointsWouldAccept()
    {
        // Arrange
        var configuration = ConfigWith(Key);

        // Act
        var token = DevelopmentScalarToken.TryCreate(configuration, Clock);
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = Issuer,
            ValidAudience = Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),
            ValidateLifetime = false
        });

        // Assert
        result.IsValid.Should().BeTrue();
        result.Claims["sub"].Should().Be("dev-user");
        result.Claims["merchant_id"].Should().Be("merchant-1");
        result.Claims["scope"].Should().Be("payments:create payments:read");
    }

    [Fact]
    public void TryCreate_Token_ExpiresInHours_NotMonths()
    {
        // Arrange
        var configuration = ConfigWith(Key);

        // Act
        var token = new JsonWebToken(DevelopmentScalarToken.TryCreate(configuration, Clock));

        // Assert
        token.ValidTo.Should().Be(Clock.GetUtcNow().UtcDateTime.AddHours(8));
    }

    [Fact]
    public void TryCreate_WithNoSigningKey_MakesNoToken()
    {
        // Arrange
        var configuration = ConfigWith(null);

        // Act
        var token = DevelopmentScalarToken.TryCreate(configuration, Clock);

        // Assert
        token.Should().BeNull("with no key configured nothing can be signed: run scripts/dev-token.ps1 once");
    }
}
#endif