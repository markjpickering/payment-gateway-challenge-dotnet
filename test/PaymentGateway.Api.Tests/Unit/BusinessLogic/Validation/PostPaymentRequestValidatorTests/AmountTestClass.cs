namespace PaymentGateway.Api.Tests.Unit.BusinessLogic.Validation.PostPaymentRequestValidatorTests;

public class AmountTestClass
{
    // Shared Arrange for every test: a validator with a fixed clock.
    private readonly PostPaymentRequestValidator _sut = ValidationTestData.NewValidator();

    [Theory]
    [InlineData(1)]
    [InlineData(1050)]
    [InlineData(int.MaxValue)]
    public void Validate_PositiveAmount_IsValid(int amount)
    {
        // Arrange
        var request = ValidationTestData.ValidRequest() with { Amount = amount };

        // Act
        var result = _sut.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_MissingAmount_IsRejected()
    {
        // Arrange
        var request = ValidationTestData.ValidRequest() with { Amount = null };

        // Act
        var result = _sut.Validate(request);

        // Assert
        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("Amount");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-1050)]
    [InlineData(int.MinValue)]
    public void Validate_ZeroOrNegativeAmount_IsRejected(int amount)
    {
        // Arrange
        var request = ValidationTestData.ValidRequest() with { Amount = amount };

        // Act
        var result = _sut.Validate(request);

        // Assert
        var error = result.Errors.Should().ContainSingle().Subject;
        error.PropertyName.Should().Be("Amount");
        error.ErrorMessage.Should().Be("Amount must be greater than zero.");
    }

    [Fact]
    public void Validate_RejectedAmount_NeverEchoesTheSubmittedValue()
    {
        // Arrange
        var request = ValidationTestData.ValidRequest() with { Amount = -987654 };

        // Act
        var result = _sut.Validate(request);

        // Assert
        result.Errors.Should().OnlyContain(e => !e.ErrorMessage.Contains("987654"));
    }
}