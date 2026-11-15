namespace PaymentGateway.Api.Tests.Unit.BusinessLogic.Validation.PostPaymentRequestValidatorTests;

public class CvvTestClass
{
    // Shared Arrange for every test: a validator with a fixed clock.
    private readonly PostPaymentRequestValidator _sut = ValidationTestData.NewValidator();

    [Theory]
    [InlineData("123")]
    [InlineData("1234")]
    [InlineData("000")]
    public void Validate_CvvOf3Or4Digits_IsValid(string cvv)
    {
        // Arrange
        var request = ValidationTestData.ValidRequest() with { Cvv = cvv };

        // Act
        var result = _sut.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("12")]
    [InlineData("12345")]
    [InlineData("12a")]
    [InlineData("1 3")]
    public void Validate_InvalidCvv_IsRejected(string cvv)
    {
        // Arrange
        var request = ValidationTestData.ValidRequest() with { Cvv = cvv };

        // Act
        var result = _sut.Validate(request);

        // Assert
        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("Cvv");
    }
}