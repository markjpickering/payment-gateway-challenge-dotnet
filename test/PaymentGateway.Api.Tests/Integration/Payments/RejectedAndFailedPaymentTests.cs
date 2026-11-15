using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace PaymentGateway.Api.Tests.Integration.Payments;

public class RejectedAndFailedPaymentTests : IClassFixture<PaymentsApiFactory>
{
    private readonly PaymentsApiFactory _factory;

    // The factory is shared through the class fixture; clear the substitute's recorded calls for each test.
    public RejectedAndFailedPaymentTests(PaymentsApiFactory factory)
    {
        _factory = factory;
        _factory.Bank.ClearReceivedCalls();
    }

    [Fact]
    public async Task Post_InvalidRequest_Returns400WithARejectedStatusAndTheFieldErrors()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = ValidationTestData.ValidRequest() with { CardNumber = "123", Currency = "XXX" };

        // Act
        var response = await client.PostAsJsonAsync("/api/payments", request, TestContext.Current.CancellationToken);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body["status"]!.GetValue<string>().Should().Be("Rejected");
        body["errors"]!.AsArray().Select(e => e!["field"]!.GetValue<string>())
            .Should().BeEquivalentTo("CardNumber", "Currency");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task Post_ZeroOrNegativeAmount_Returns400WithARejectedStatusAndNeverCallsTheBank(int amount)
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = ValidationTestData.ValidRequest() with { Amount = amount };

        // Act
        var response = await client.PostAsJsonAsync("/api/payments", request, TestContext.Current.CancellationToken);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body["status"]!.GetValue<string>().Should().Be("Rejected");
        body["errors"]!.AsArray().Should().ContainSingle().Which!["field"]!.GetValue<string>().Should().Be("Amount");
        await _factory.Bank.DidNotReceiveWithAnyArgs().AuthorizeAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Post_InvalidRequest_NeverCallsTheBank()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = ValidationTestData.ValidRequest() with { Cvv = "1" };

        // Act
        await client.PostAsJsonAsync("/api/payments", request, TestContext.Current.CancellationToken);

        // Assert
        await _factory.Bank.DidNotReceiveWithAnyArgs().AuthorizeAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Post_InvalidRequest_NeverEchoesSubmittedValuesInTheResponse()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = ValidationTestData.ValidRequest() with { CardNumber = "12AB", Cvv = "9x9x9" };

        // Act
        var response = await client.PostAsJsonAsync("/api/payments", request, TestContext.Current.CancellationToken);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        text.Should().NotContain("12AB").And.NotContain("9x9x9");
    }

    [Fact]
    public async Task Post_BankError_Returns502ProblemDetailsWithATraceId()
    {
        // Arrange
        _factory.Bank.Fails();
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/payments", ValidationTestData.ValidRequest(), TestContext.Current.CancellationToken);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        body["status"]!.GetValue<int>().Should().Be(502);
        body["traceId"].Should().NotBeNull();
    }
}