using System.Net;
using System.Net.Http.Json;

using Microsoft.IdentityModel.Tokens;

namespace PaymentGateway.Api.Tests.Integration.Authentication;

public class AuthenticationTests : IClassFixture<PaymentsApiFactory>
{
    private static readonly string GetPath = $"/api/payments/{Guid.NewGuid()}";
    private static readonly SymmetricSecurityKey WrongKey = new(new byte[32]);

    private readonly PaymentsApiFactory _factory;

    public AuthenticationTests(PaymentsApiFactory factory)
    {
        _factory = factory;
        _factory.Bank.ClearReceivedCalls();
    }

    public static TheoryData<string, bool> InvalidTokens => new()
    {
        { "not-a-jwt", true },
        { "not-a-jwt", false },
        { TestTokens.For(key: WrongKey), true },
        { TestTokens.For(key: WrongKey), false },
        { TestTokens.For(lifetime: TimeSpan.FromHours(-1)), true },
        { TestTokens.For(lifetime: TimeSpan.FromHours(-1)), false }
    };

    private HttpClient ClientWith(string? token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = token is null ? null : new("Bearer", token);

        return client;
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, bool post) => post
        ? client.PostAsJsonAsync("/api/payments", ValidationTestData.ValidRequest(), TestContext.Current.CancellationToken)
        : client.GetAsync(GetPath, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Request_WithNoToken_Returns401(bool post)
    {
        // Arrange
        var client = ClientWith(null);

        // Act
        var response = await SendAsync(client, post);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [MemberData(nameof(InvalidTokens))]
    public async Task Request_WithAnInvalidToken_Returns401(string token, bool post)
    {
        // Arrange
        var client = ClientWith(token);

        // Act
        var response = await SendAsync(client, post);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(true, TestTokens.Read)]
    [InlineData(false, TestTokens.Create)]
    public async Task Request_WithoutTheRequiredScope_Returns403(bool post, string scope)
    {
        // Arrange
        var client = ClientWith(TestTokens.For(scopes: [scope]));

        // Act
        var response = await SendAsync(client, post);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_WithoutAToken_NeverCallsTheBank()
    {
        // Arrange
        var client = ClientWith(null);

        // Act
        await SendAsync(client, post: true);

        // Assert
        await _factory.Bank.DidNotReceiveWithAnyArgs().AuthorizeAsync(default!, TestContext.Current.CancellationToken);
    }
}