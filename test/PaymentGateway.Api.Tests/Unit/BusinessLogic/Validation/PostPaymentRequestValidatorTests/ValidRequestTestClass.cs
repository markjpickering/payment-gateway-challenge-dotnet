namespace PaymentGateway.Api.Tests.Unit.BusinessLogic.Validation.PostPaymentRequestValidatorTests;

public class ValidRequestTestClass
{
    // Shared Arrange for every test: a validator with a fixed clock.
    private readonly PostPaymentRequestValidator _sut = ValidationTestData.NewValidator();

    [Fact]
    public void Validate_ValidRequest_IsValid()
    {
        // Arrange
        var request = ValidationTestData.ValidRequest();

        // Act
        var result = _sut.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyRequest_ReportsEveryRequiredField()
    {
        // Arrange
        var request = new PostPaymentRequest();

        // Act
        var result = _sut.Validate(request);

        // Assert
        result.Errors.Select(e => e.PropertyName).Should().BeEquivalentTo(
            "CardNumber", "ExpiryMonth", "ExpiryYear", "Currency", "Amount", "Cvv");
    }

    [Fact]
    public void Validate_InvalidRequest_NeverEchoesSubmittedValuesInMessages()
    {
        // Arrange
        var request = ValidationTestData.ValidRequest() with { CardNumber = "12AB", Cvv = "9x9x9" };

        // Act
        var result = _sut.Validate(request);

        // Assert
        result.Errors.Select(e => e.ErrorMessage).Should()
            .NotContain(m => m.Contains("12AB") || m.Contains("9x9x9"));
    }
}