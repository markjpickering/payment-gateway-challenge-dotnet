using System.Net;
using System.Net.Http.Json;

using Microsoft.Extensions.DependencyInjection;

namespace PaymentGateway.Api.Tests.Integration.Payments;

public class GetPaymentTests : IClassFixture<PaymentsApiFactory>
{
    private readonly PaymentsApiFactory _factory;

    // The factory is shared by the tests in this class through the class fixture (ADR-6); the tests
    // only use unique payment ids, so they cannot affect each other.
    public GetPaymentTests(PaymentsApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Get_ExistingPayment_ReturnsItWithTheLastFourDigits()
    {
        // Arrange
        var payment = new PaymentEntity(
            Guid.NewGuid(), TestTokens.DefaultSubject, PaymentStatus.Authorized, "0042", 4, 2030, "GBP", 1050);
        var repository = _factory.Services.GetRequiredService<IPaymentsRepository>();
        await repository.AddAsync(payment, TestContext.Current.CancellationToken);
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/payments/{payment.Id}", TestContext.Current.CancellationToken);
        var paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>(TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        paymentResponse.Should().BeEquivalentTo(new
        {
            payment.Id,
            payment.Status,
            payment.CardNumberLastFour,
            payment.ExpiryMonth,
            payment.ExpiryYear,
            payment.Currency,
            payment.Amount
        });
        paymentResponse!.CardNumberLastFour.Should().Be("0042");
    }

    [Fact]
    public async Task Get_UnknownPayment_Returns404()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/payments/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_IdThatIsNotAGuid_Returns404()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/payments/not-a-guid", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}