using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;

namespace PaymentGateway.Api.Tests.Unit.Api.Security;

public class ScopeAuthorizationHandlerTests
{
    private readonly ScopeAuthorizationHandler _sut = new();

    private async Task<bool> SucceedsAsync(string requiredScope, params Claim[] claims)
    {
        var requirement = new ScopeRequirement(requiredScope);
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        await _sut.HandleAsync(context);

        return context.HasSucceeded;
    }

    [Theory]
    [InlineData("payments:create")]
    [InlineData("payments:read payments:create")]
    [InlineData("payments:read payments:create payments:reverse")]
    public async Task Handle_ScopeClaimContainsTheScope_Succeeds(string scopeClaim)
    {
        // Arrange: done in the constructor and SucceedsAsync

        // Act
        var succeeded = await SucceedsAsync("payments:create", new Claim("scope", scopeClaim));

        // Assert
        succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData("payments:read")]
    [InlineData("payments:create:extra")]
    [InlineData("xpayments:create")]
    [InlineData("")]
    public async Task Handle_ScopeClaimLacksTheScope_DoesNotSucceed(string scopeClaim)
    {
        // Arrange: done in the constructor and SucceedsAsync

        // Act
        var succeeded = await SucceedsAsync("payments:create", new Claim("scope", scopeClaim));

        // Assert
        succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NoScopeClaim_DoesNotSucceed()
    {
        // Arrange: done in the constructor and SucceedsAsync

        // Act
        var succeeded = await SucceedsAsync("payments:create", new Claim("sub", "someone"));

        // Assert
        succeeded.Should().BeFalse();
    }
}