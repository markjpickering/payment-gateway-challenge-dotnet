using System.Net.Http.Json;

namespace PaymentGateway.Api.Tests.Integration.Logging;

public class PaymentLoggingTests : IClassFixture<PaymentsApiFactory>
{
    private const string CardNumber = "2222405343248877";
    private const string Cvv = "4096";

    private readonly PaymentsApiFactory _factory;

    public PaymentLoggingTests(PaymentsApiFactory factory)
    {
        _factory = factory;
        _factory.Bank.ClearReceivedCalls();
    }

    private Task<HttpResponseMessage> PostPaymentAsync() =>
        _factory.CreateClient().PostAsJsonAsync(
            "/api/payments",
            ValidationTestData.ValidRequest() with { CardNumber = CardNumber, Cvv = Cvv },
            TestContext.Current.CancellationToken);

    [Fact]
    public async Task Post_AuthorizedPayment_LogsThePaymentIdAndStatusWithATraceId()
    {
        // Arrange
        _factory.Bank.Authorizes();

        // Act
        var response = await PostPaymentAsync();
        var id = (await response.Content.ReadFromJsonAsync<PostPaymentResponse>(TestContext.Current.CancellationToken))!.Id;

        // Assert
        var logged = _factory.Logs.Events.Single(e => e.Properties.ContainsKey("PaymentId") && e.Properties["PaymentId"].ToString().Trim('"') == id.ToString());
        logged.Properties["Status"].ToString().Trim('"').Should().Be("Authorized");
        logged.TraceId.Should().NotBeNull();
    }

    [Theory]
    [InlineData("authorized")]
    [InlineData("declined")]
    [InlineData("failed")]
    public async Task Post_AnyOutcome_NeverLogsTheCardNumberOrCvv(string outcome)
    {
        // Arrange
        switch (outcome)
        {
            case "authorized": _factory.Bank.Authorizes(); break;
            case "declined": _factory.Bank.Declines(); break;
            default: _factory.Bank.Fails(); break;
        }

        // Act
        await PostPaymentAsync();

        // Assert
        _factory.Logs.RenderedEvents().Should().OnlyContain(text => !text.Contains(CardNumber) && !text.Contains(Cvv));
    }
}