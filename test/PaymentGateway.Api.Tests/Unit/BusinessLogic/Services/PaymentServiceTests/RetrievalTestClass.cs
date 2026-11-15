namespace PaymentGateway.Api.Tests.Unit.BusinessLogic.Services.PaymentServiceTests;

/// <summary>Who may see a payment is a business rule, so it is decided here and not in the repository (ADR-26).</summary>
public class RetrievalTestClass
{
    private readonly PaymentServiceTestContext _context = new();

    private PaymentEntity GivenAPaymentEntityOf(string merchantId)
    {
        var stored = new PaymentEntity(Guid.NewGuid(), merchantId, PaymentStatus.Authorized, "8877", 4, 2030, "GBP", 100);
        _context.Repository.GetAsync(stored.Id, TestContext.Current.CancellationToken).Returns(stored);

        return stored;
    }

    [Fact]
    public async Task GetAsync_PaymentOfTheCallersMerchant_ReturnsItAsAResponse()
    {
        // Arrange
        var stored = GivenAPaymentEntityOf(PaymentServiceTestContext.MerchantId);

        // Act
        var result = await _context.Sut.GetAsync(stored.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEquivalentTo(new
        {
            Id = stored.Id,
            Status = PaymentStatus.Authorized,
            CardNumberLastFour = "8877",
            ExpiryMonth = 4,
            ExpiryYear = 2030,
            Currency = "GBP",
            Amount = 100
        });
    }

    [Fact]
    public async Task GetAsync_PaymentOfAnotherMerchant_ReturnsNullAsIfItDidNotExist()
    {
        // Arrange
        var stored = GivenAPaymentEntityOf("another-merchant");

        // Act
        var result = await _context.Sut.GetAsync(stored.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_UnknownPayment_ReturnsNull()
    {
        // Arrange: the substituted repository returns null for anything it was not told about

        // Act
        var result = await _context.Sut.GetAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }
}