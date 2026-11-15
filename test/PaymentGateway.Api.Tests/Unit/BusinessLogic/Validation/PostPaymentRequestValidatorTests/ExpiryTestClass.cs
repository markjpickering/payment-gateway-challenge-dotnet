namespace PaymentGateway.Api.Tests.Unit.BusinessLogic.Validation.PostPaymentRequestValidatorTests;

// The fixed clock is 14 November 2026.
public class ExpiryTestClass
{
    // Shared Arrange for every test: a validator with a fixed clock.
    private readonly PostPaymentRequestValidator _sut = ValidationTestData.NewValidator();

    [Theory]
    [InlineData(1, 2027)]
    [InlineData(12, 2026)]
    [InlineData(11, 2026)] // the current month is valid until the end of that month
    [InlineData(12, 9999)]
    public void Validate_ExpiryNotYetPassed_IsValid(int month, int year)
    {
        // Arrange
        var request = ValidationTestData.ValidRequest() with { ExpiryMonth = month, ExpiryYear = year };

        // Act
        var result = _sut.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(10, 2026)] // last month
    [InlineData(12, 2025)]
    [InlineData(1, 1999)]
    public void Validate_ExpiryInThePast_IsRejected(int month, int year)
    {
        // Arrange
        var request = ValidationTestData.ValidRequest() with { ExpiryMonth = month, ExpiryYear = year };

        // Act
        var result = _sut.Validate(request);

        // Assert
        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("ExpiryYear");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public void Validate_MonthOutOfRange_IsRejected(int month)
    {
        // Arrange
        var request = ValidationTestData.ValidRequest() with { ExpiryMonth = month };

        // Act
        var result = _sut.Validate(request);

        // Assert
        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("ExpiryMonth");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10000)]
    [InlineData(-2030)]
    public void Validate_YearOutOfRange_IsRejected(int year)
    {
        // Arrange
        var request = ValidationTestData.ValidRequest() with { ExpiryYear = year };

        // Act
        var result = _sut.Validate(request);

        // Assert
        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("ExpiryYear");
    }

    [Fact]
    public void Validate_MissingMonthAndYear_ReportsBoth()
    {
        // Arrange
        var request = ValidationTestData.ValidRequest() with { ExpiryMonth = null, ExpiryYear = null };

        // Act
        var result = _sut.Validate(request);

        // Assert
        result.Errors.Select(e => e.PropertyName).Should().BeEquivalentTo("ExpiryMonth", "ExpiryYear");
    }
}