namespace PaymentGateway.Api.Tests.Unit.Shared.Outcomes;

public class ResultTests
{
    [Fact]
    public void Success_WithValue_IsSuccessAndHoldsTheValue()
    {
        // Arrange
        const int value = 42;

        // Act
        var result = Result<int>.Success(value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(value);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failure_WithError_IsNotSuccessAndHoldsTheError()
    {
        // Arrange
        var error = new Error(ErrorKind.BankError, "The bank could not be reached.");

        // Act
        var result = Result<int>.Failure(error);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(error);
        result.Value.Should().Be(default);
    }
}