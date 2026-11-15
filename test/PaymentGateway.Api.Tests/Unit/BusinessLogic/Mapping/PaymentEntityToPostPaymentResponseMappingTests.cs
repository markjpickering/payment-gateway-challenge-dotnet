namespace PaymentGateway.Api.Tests.Unit.BusinessLogic.Mapping;

public class PaymentEntityToPostPaymentResponseMappingTests
{
    // Shared Arrange for every test: the mapping under test.
    private readonly PaymentEntityToPostPaymentResponseMapping _sut = new();

    private static PaymentEntity NewPaymentEntity() => new(
        Guid.NewGuid(), "merchant-1", PaymentStatus.Declined, "0042", 4, 2030, "GBP", 1050);

    [Fact]
    public void Map_PaymentEntity_CopiesEveryFieldTheMerchantSees()
    {
        // Arrange
        var stored = NewPaymentEntity();

        // Act
        var response = _sut.Map(stored);

        // Assert
        response.Should().BeEquivalentTo(new
        {
            Id = stored.Id,
            Status = PaymentStatus.Declined,
            CardNumberLastFour = "0042",
            ExpiryMonth = 4,
            ExpiryYear = 2030,
            Currency = "GBP",
            Amount = 1050
        });
    }

    [Fact]
    public void Map_PaymentEntity_NeverExposesTheMerchant()
    {
        // Arrange
        var stored = NewPaymentEntity();

        // Act
        var properties = _sut.Map(stored).GetType().GetProperties().Select(p => p.Name);

        // Assert
        properties.Should().NotContain("MerchantId");
    }
}