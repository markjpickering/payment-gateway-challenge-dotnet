using System.Security.Claims;

using Microsoft.AspNetCore.Http;

namespace PaymentGateway.Api.Tests.Unit.Api.Security;

public class CallerIdentityTests
{
    private static CallerIdentity For(params Claim[] claims)
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) });

        return new CallerIdentity(accessor);
    }

    [Fact]
    public void InitiatedBy_IsTheSubjectClaim()
    {
        // Arrange
        var sut = For(new Claim("sub", "support-agent"), new Claim("merchant_id", "merchant-1"));

        // Act
        var initiatedBy = sut.InitiatedBy;

        // Assert
        initiatedBy.Should().Be("support-agent");
    }

    [Theory]
    [InlineData("merchant_id", "merchant-1")]
    [InlineData("client_id", "client-1")]
    [InlineData("azp", "authorized-party-1")]
    public void OnBehalfOf_UsesTheMerchantClaimThenTheClientIdentifiers(string claimType, string value)
    {
        // Arrange
        var sut = For(new Claim("sub", "someone"), new Claim(claimType, value));

        // Act
        var onBehalfOf = sut.OnBehalfOf;

        // Assert
        onBehalfOf.Should().Be(value);
    }

    [Fact]
    public void OnBehalfOf_PrefersTheMerchantClaimOverTheClientId()
    {
        // Arrange
        var sut = For(new Claim("sub", "someone"), new Claim("client_id", "client-1"), new Claim("merchant_id", "merchant-1"));

        // Act
        var onBehalfOf = sut.OnBehalfOf;

        // Assert
        onBehalfOf.Should().Be("merchant-1");
    }

    [Fact]
    public void OnBehalfOf_WithNoMerchantOrClientClaim_IsTheSubject()
    {
        // Arrange
        var sut = For(new Claim("sub", "merchant-acting-for-itself"));

        // Act
        var onBehalfOf = sut.OnBehalfOf;

        // Assert
        onBehalfOf.Should().Be("merchant-acting-for-itself");
    }
}