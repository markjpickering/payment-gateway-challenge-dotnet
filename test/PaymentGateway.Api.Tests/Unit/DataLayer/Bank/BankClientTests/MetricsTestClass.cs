using System.Net;

using Microsoft.Extensions.Time.Testing;

using PaymentGateway.DataInterface.Telemetry;

namespace PaymentGateway.Api.Tests.Unit.DataLayer.Bank.BankClientTests;

public class MetricsTestClass
{
    private readonly IPaymentMetrics _metrics = Substitute.For<IPaymentMetrics>();

    [Theory]
    [InlineData("""{"authorized": true, "authorization_code": "x"}""", BankOutcome.Authorized)]
    [InlineData("""{"authorized": false, "authorization_code": ""}""", BankOutcome.Declined)]
    public async Task AuthorizeAsync_BankAnswers_RecordsTheOutcome(string body, BankOutcome expected)
    {
        // Arrange
        var sut = BankClientTestData.NewClient(StubHttpMessageHandler.Responding(HttpStatusCode.OK, body), _metrics);

        // Act
        await sut.AuthorizeAsync(BankClientTestData.ValidBankRequest(), TestContext.Current.CancellationToken);

        // Assert
        _metrics.Received(1).BankRequest(expected, Arg.Any<TimeSpan>());
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task AuthorizeAsync_BankFails_RecordsAnError(HttpStatusCode status)
    {
        // Arrange
        var sut = BankClientTestData.NewClient(StubHttpMessageHandler.Responding(status, string.Empty), _metrics);

        // Act
        await sut.AuthorizeAsync(BankClientTestData.ValidBankRequest(), TestContext.Current.CancellationToken);

        // Assert
        _metrics.Received(1).BankRequest(BankOutcome.Error, Arg.Any<TimeSpan>());
    }

    [Fact]
    public async Task AuthorizeAsync_RecordsHowLongTheCallTook()
    {
        // Arrange: the fake clock moves 300 ms while the stub bank "responds"
        var clock = new FakeTimeProvider(ValidationTestData.Now);
        var handler = StubHttpMessageHandler.Responding(HttpStatusCode.OK, """{"authorized": true, "authorization_code": "x"}""");
        handler.OnSend = () => clock.Advance(TimeSpan.FromMilliseconds(300));
        var sut = BankClientTestData.NewClient(handler, _metrics, clock);

        // Act
        await sut.AuthorizeAsync(BankClientTestData.ValidBankRequest(), TestContext.Current.CancellationToken);

        // Assert
        _metrics.Received(1).BankRequest(BankOutcome.Authorized, TimeSpan.FromMilliseconds(300));
    }
}