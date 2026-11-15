namespace PaymentGateway.Api.Tests.Unit.BusinessLogic.Validation.PostPaymentRequestValidatorTests;

public class CardNumberTestClass
{
    // Shared Arrange for every test: a validator with a fixed clock.
    private readonly PostPaymentRequestValidator _sut = ValidationTestData.NewValidator();

    [Theory]
    [InlineData("12345678901234")]
    [InlineData("1234567890123456789")]
    [InlineData("0000000000000000")]
    public void Validate_CardNumberOf14To19Digits_IsValid(string cardNumber)
    {
        // Arrange
        var request = ValidationTestData.ValidRequest() with { CardNumber = cardNumber };

        // Act
        var result = _sut.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234567890123")]
    [InlineData("12345678901234567890")]
    [InlineData("1234 5678 9012 3456")]
    [InlineData("12345678901234ab")]
    [InlineData("1234-5678-9012-34")]
    public void Validate_InvalidCardNumber_IsRejected(string cardNumber)
    {
        // Arrange
        var request = ValidationTestData.ValidRequest() with { CardNumber = cardNumber };

        // Act
        var result = _sut.Validate(request);

        // Assert
        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("CardNumber");
    }
}