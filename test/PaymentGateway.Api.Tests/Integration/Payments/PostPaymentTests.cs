using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace PaymentGateway.Api.Tests.Integration.Payments;

public class PostPaymentTests : IClassFixture<PaymentsApiFactory>
{
    private readonly PaymentsApiFactory _factory;

    // The factory is shared through the class fixture; xUnit creates this class once per test, so the
    // substitute's recorded calls are cleared before every test.
    public PostPaymentTests(PaymentsApiFactory factory)
    {
        _factory = factory;
        _factory.Bank.ClearReceivedCalls();
    }

    [Fact]
    public async Task Post_BankAuthorizes_ReturnsOkWithAnAuthorizedPayment()
    {
        // Arrange
        _factory.Bank.Authorizes();
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/payments", ValidationTestData.ValidRequest(), TestContext.Current.CancellationToken);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body["status"]!.GetValue<string>().Should().Be("Authorized");
        body["cardNumberLastFour"]!.GetValue<string>().Should().Be("8877");
        body["expiryMonth"]!.GetValue<int>().Should().Be(4);
        body["expiryYear"]!.GetValue<int>().Should().Be(2030);
        body["currency"]!.GetValue<string>().Should().Be("GBP");
        body["amount"]!.GetValue<int>().Should().Be(100);
        Guid.Parse(body["id"]!.GetValue<string>()).Should().NotBeEmpty();
    }

    [Fact]
    public async Task Post_BankDeclines_ReturnsOkWithADeclinedPayment()
    {
        // Arrange
        _factory.Bank.Declines();
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/payments", ValidationTestData.ValidRequest(), TestContext.Current.CancellationToken);
        var payment = await response.Content.ReadFromJsonAsync<PostPaymentResponse>(TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        payment!.Status.Should().Be(PaymentStatus.Declined);
    }

    [Fact]
    public async Task Post_AuthorizedPayment_CanBeRetrievedWithGet()
    {
        // Arrange
        _factory.Bank.Authorizes();
        var client = _factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/payments", ValidationTestData.ValidRequest(), TestContext.Current.CancellationToken);
        var payment = await created.Content.ReadFromJsonAsync<PostPaymentResponse>(TestContext.Current.CancellationToken);

        // Act
        var response = await client.GetAsync($"/api/payments/{payment!.Id}", TestContext.Current.CancellationToken);
        var retrieved = await response.Content.ReadFromJsonAsync<PostPaymentResponse>(TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        retrieved.Should().BeEquivalentTo(payment);
    }

    [Fact]
    public async Task Post_ValidRequest_SendsTheBankTheMappedRequest()
    {
        // Arrange
        _factory.Bank.Authorizes();
        var client = _factory.CreateClient();

        // Act
        await client.PostAsJsonAsync("/api/payments", ValidationTestData.ValidRequest(), TestContext.Current.CancellationToken);

        // Assert
        await _factory.Bank.Received(1).AuthorizeAsync(
            Arg.Is<BankPaymentRequest>(r => r.CardNumber == "2222405343248877" && r.ExpiryDate == "04/2030"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Post_Response_NeverContainsTheCardNumberOrCvv()
    {
        // Arrange
        _factory.Bank.Authorizes();
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/payments", ValidationTestData.ValidRequest(), TestContext.Current.CancellationToken);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        text.Should().NotContain("2222405343248877");
        JsonNode.Parse(text)!.AsObject().Select(p => p.Key).Should().NotContain(["cvv", "cardNumber"]);
    }
}