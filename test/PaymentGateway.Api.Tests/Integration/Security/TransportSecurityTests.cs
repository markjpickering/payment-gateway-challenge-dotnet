using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace PaymentGateway.Api.Tests.Integration.Security;

public class TransportSecurityTests : IClassFixture<PaymentsApiFactory>
{
    private readonly PaymentsApiFactory _factory;

    public TransportSecurityTests(PaymentsApiFactory factory) => _factory = factory;

    private async Task<HttpResponseMessage> GetOverHttpsAsync(string environment)
    {
        using var factory = _factory.WithWebHostBuilder(builder => builder.UseEnvironment(environment));
        var client = factory.CreateClient(new() { BaseAddress = new Uri("https://example.com"), AllowAutoRedirect = false });

        return await client.GetAsync($"/api/payments/{Guid.NewGuid()}", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Response_OutsideDevelopment_AsksBrowsersToUseHttpsOnly()
    {
        // Arrange: done in the helper

        // Act
        var response = await GetOverHttpsAsync("Production");

        // Assert
        response.Headers.Should().ContainKey("Strict-Transport-Security");
    }

    [Fact]
    public async Task Response_InDevelopment_DoesNotSendHstsSoLocalhostIsNotPinnedToHttps()
    {
        // Arrange: done in the helper

        // Act
        var response = await GetOverHttpsAsync("Development");

        // Assert
        response.Headers.Should().NotContainKey("Strict-Transport-Security");
    }

    [Fact]
    public async Task PlainHttpRequest_IsRedirectedToHttps()
    {
        // Arrange
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting("HTTPS_PORT", "443").UseEnvironment("Production"));
        var client = factory.CreateClient(new() { BaseAddress = new Uri("http://example.com"), AllowAutoRedirect = false });

        // Act
        var response = await client.GetAsync($"/api/payments/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.RedirectKeepVerb);
        response.Headers.Location!.Scheme.Should().Be("https");
    }

    [Fact]
    public async Task AnEndpointWithNoAuthorizationRequirement_StillRequiresASignedInUser()
    {
        // Arrange: a new endpoint that forgets .RequireAuthorization() must not be public by accident
        var provider = _factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        // Act
        var fallback = await provider.GetFallbackPolicyAsync();

        // Assert
        fallback.Should().NotBeNull();
        fallback!.Requirements.Should().NotBeEmpty();
    }
}