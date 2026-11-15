using System.Net;
using System.Text;
using System.Text.Json.Nodes;

using Microsoft.AspNetCore.Hosting;

namespace PaymentGateway.Api.Tests.Integration.Payments;

public class MalformedRequestTests : IClassFixture<PaymentsApiFactory>
{
    private readonly PaymentsApiFactory _factory;

    // The factory is shared through the class fixture; clear the substitute's recorded calls for each test.
    public MalformedRequestTests(PaymentsApiFactory factory)
    {
        _factory = factory;
        _factory.Bank.ClearReceivedCalls();
    }

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("")]
    [InlineData("""{"cardNumber":"2222405343248877","expiryMonth":"abc","expiryYear":2030,"currency":"GBP","amount":100,"cvv":"123"}""")]
    [InlineData("""{"cardNumber":"2222405343248877","expiryMonth":4,"expiryYear":2030,"currency":"GBP","amount":"1.5","cvv":"123"}""")]
    public async Task Post_MalformedJsonBody_Returns400WithARejectedStatus(string json)
    {
        // Arrange
        var client = _factory.CreateClient();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/payments", content, TestContext.Current.CancellationToken);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body["status"]!.GetValue<string>().Should().Be("Rejected");
        body["errors"]!.AsArray().Should().NotBeEmpty();
    }

    [Fact]
    public async Task Post_MalformedJsonBody_NeverCallsTheBank()
    {
        // Arrange
        var client = _factory.CreateClient();
        using var content = new StringContent("{ this is not json", Encoding.UTF8, "application/json");

        // Act
        await client.PostAsync("/api/payments", content, TestContext.Current.CancellationToken);

        // Assert
        await _factory.Bank.DidNotReceiveWithAnyArgs().AuthorizeAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Post_BodyThatIsNotJson_Returns415()
    {
        // Arrange
        var client = _factory.CreateClient();
        using var content = new StringContent("cardNumber=2222405343248877", Encoding.UTF8, "text/plain");

        // Act
        var response = await client.PostAsync("/api/payments", content, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task Post_MalformedJsonBody_InProduction_StillReturns400WithARejectedStatus()
    {
        // Arrange: the framework only throws binding failures by default in Development, so check Production too
        using var productionFactory = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        var client = productionFactory.CreateClient();
        using var content = new StringContent("{ this is not json", Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/payments", content, TestContext.Current.CancellationToken);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body["status"]!.GetValue<string>().Should().Be("Rejected");
    }
}