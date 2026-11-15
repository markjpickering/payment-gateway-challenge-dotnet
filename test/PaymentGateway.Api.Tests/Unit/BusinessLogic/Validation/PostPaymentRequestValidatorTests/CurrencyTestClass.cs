namespace PaymentGateway.Api.Tests.Unit.BusinessLogic.Validation.PostPaymentRequestValidatorTests;

public class CurrencyTestClass
{
    // Shared Arrange for every test: a validator with a fixed clock.
    private readonly PostPaymentRequestValidator _sut = ValidationTestData.NewValidator();

    [Theory]
    [InlineData("USD")]
    [InlineData("GBP")]
    [InlineData("EUR")]
    public void Validate_AllowedCurrency_IsValid(string currency)
    {
        // Arrange
        var request = ValidationTestData.ValidRequest() with { Currency = currency };

        // Act
        var result = _sut.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("JPY")]
    [InlineData("gbp")]
    [InlineData("GB")]
    [InlineData("GBPP")]
    public void Validate_CurrencyNotOnTheAllowlist_IsRejected(string currency)
    {
        // Arrange
        var request = ValidationTestData.ValidRequest() with { Currency = currency };

        // Act
        var result = _sut.Validate(request);

        // Assert
        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("Currency");
    }
}