using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc.Testing;

namespace PaymentGateway.Api.Tests.Integration.Authentication;

/// <summary>With no signing keys configured the API is closed by default: no token is accepted (ADR-7).</summary>
public class UnconfiguredAuthenticationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public UnconfiguredAuthenticationTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Post_WithATokenWhenNoKeysAreConfigured_Returns401()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", TestTokens.For());

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/payments", ValidationTestData.ValidRequest(), TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}