using System.Text.Json;

using PaymentGateway.BusinessLogic.Models;

namespace PaymentGateway.Api.Tests.Unit.BusinessLogic.Mapping;

public class ProcessedPaymentToPaymentEntityMappingTests
{
    // A fixed id, because a random GUID could contain the CVV's digits by chance.
    private static readonly Guid PaymentId = Guid.Parse("a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d");

    // Shared Arrange for every test: the mapping under test.
    private readonly ProcessedPaymentToPaymentEntityMapping _sut = new();

    private static ProcessedPaymentModel NewPayment(string cardNumber = "2222405343248877") => new(
        PaymentId, "merchant-1", PaymentStatus.Authorized, ValidationTestData.ValidRequest() with { CardNumber = cardNumber });

    [Fact]
    public void Map_Payment_CopiesIdMerchantStatusExpiryCurrencyAndAmount()
    {
        // Arrange
        var payment = NewPayment();

        // Act
        var stored = _sut.Map(payment);

        // Assert
        stored.Should().BeEquivalentTo(new
        {
            Id = PaymentId,
            MerchantId = "merchant-1",
            Status = PaymentStatus.Authorized,
            ExpiryMonth = payment.Request.ExpiryMonth,
            ExpiryYear = payment.Request.ExpiryYear,
            Currency = payment.Request.Currency,
            Amount = payment.Request.Amount
        });
    }

    [Theory]
    [InlineData("2222405343248877", "8877")]
    [InlineData("12345678901234", "1234")]
    [InlineData("1111222233330042", "0042")]
    public void Map_CardNumber_KeepsOnlyTheLastFourDigitsAsAString(string cardNumber, string expectedLastFour)
    {
        // Arrange
        var payment = NewPayment(cardNumber);

        // Act
        var stored = _sut.Map(payment);

        // Assert
        stored.CardNumberLastFour.Should().Be(expectedLastFour);
    }

    [Fact]
    public void Map_Payment_NeverStoresTheFullCardNumberOrTheCvv()
    {
        // Arrange
        var payment = NewPayment();

        // Act
        var json = JsonSerializer.Serialize(_sut.Map(payment));

        // Assert
        json.Should().NotContain(payment.Request.CardNumber).And.NotContain($"\"{payment.Request.Cvv}\"");
    }
}