namespace PaymentGateway.Api.Tests.Unit.BusinessLogic.Mapping;

public class PostPaymentRequestToBankPaymentRequestMappingTests
{
    // Shared Arrange for every test: the mapping under test.
    private readonly PostPaymentRequestToBankPaymentRequestMapping _sut = new();

    [Fact]
    public void Map_ValidRequest_CopiesCardNumberCurrencyAmountAndCvv()
    {
        // Arrange
        var request = ValidationTestData.ValidRequest();

        // Act
        var result = _sut.Map(request);

        // Assert
        result.CardNumber.Should().Be(request.CardNumber);
        result.Currency.Should().Be(request.Currency);
        result.Amount.Should().Be(request.Amount);
        result.Cvv.Should().Be(request.Cvv);
    }

    [Theory]
    [InlineData(4, 2030, "04/2030")]
    [InlineData(12, 2026, "12/2026")]
    [InlineData(1, 999, "01/0999")]
    public void Map_ExpiryMonthAndYear_FormatsAsZeroPaddedMonthSlashYear(int month, int year, string expected)
    {
        // Arrange
        var request = ValidationTestData.ValidRequest() with { ExpiryMonth = month, ExpiryYear = year };

        // Act
        var result = _sut.Map(request);

        // Assert
        result.ExpiryDate.Should().Be(expected);
    }
}