namespace PaymentGateway.Api.Tests.Unit.BusinessLogic.Services.PaymentServiceTests;

public class RejectedPaymentTestClass
{
    // Shared Arrange for every test: the service with a substituted bank and repository.
    private readonly PaymentServiceTestContext _context = new();

    [Fact]
    public async Task ProcessAsync_InvalidRequest_ReturnsValidationFailedWithTheFieldErrors()
    {
        // Arrange
        var request = ValidationTestData.ValidRequest() with { CardNumber = "123", Cvv = "1" };

        // Act
        var result = await _context.Sut.ProcessAsync(request, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.ValidationFailed);
        result.Error!.FieldErrors.Select(e => e.Field).Should().BeEquivalentTo("CardNumber", "Cvv");
    }

    [Fact]
    public async Task ProcessAsync_InvalidRequest_NeverCallsTheBank()
    {
        // Arrange
        var request = ValidationTestData.ValidRequest() with { Currency = "XXX" };

        // Act
        await _context.Sut.ProcessAsync(request, TestContext.Current.CancellationToken);

        // Assert
        await _context.Bank.DidNotReceiveWithAnyArgs().AuthorizeAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ProcessAsync_InvalidRequest_StoresNothing()
    {
        // Arrange
        var request = ValidationTestData.ValidRequest() with { Amount = null };

        // Act
        await _context.Sut.ProcessAsync(request, TestContext.Current.CancellationToken);

        // Assert
        await _context.Repository.DidNotReceiveWithAnyArgs().AddAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ProcessAsync_InvalidRequest_NeverEchoesSubmittedValuesInTheErrors()
    {
        // Arrange
        var request = ValidationTestData.ValidRequest() with { CardNumber = "12AB", Cvv = "9x9x9" };

        // Act
        var result = await _context.Sut.ProcessAsync(request, TestContext.Current.CancellationToken);

        // Assert
        result.Error!.FieldErrors.Select(e => e.Message).Should()
            .NotContain(m => m.Contains("12AB") || m.Contains("9x9x9"));
    }
}