using PaymentGateway.DataInterface.Telemetry;
using PaymentGateway.DataLayer.Placeholders;

namespace PaymentGateway.Api.Tests.Unit.DataLayer.Placeholders;

/// <summary>The metrics placeholder must be harmless: recording anything never throws, so it can never fail a payment.</summary>
public class NoOpPaymentMetricsTests
{
    [Fact]
    public void RecordingAnything_DoesNotThrow()
    {
        // Arrange
        var sut = new NoOpPaymentMetrics();

        // Act
        var act = () =>
        {
            sut.PaymentProcessed(PaymentStatus.Authorized);
            sut.BankRequest(BankOutcome.Error, TimeSpan.FromSeconds(1));
        };

        // Assert
        act.Should().NotThrow();
    }
}