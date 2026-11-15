namespace PaymentGateway.Api.Tests.Unit.BusinessLogic.Services.PaymentServiceTests;

public class BankFailureTestClass
{
    // Shared Arrange for every test: the service with a substituted bank and repository.
    private readonly PaymentServiceTestContext _context = new();

    [Fact]
    public async Task ProcessAsync_BankFails_ReturnsTheBankError()
    {
        // Arrange
        _context.BankFails();

        // Act
        var result = await _context.Sut.ProcessAsync(ValidationTestData.ValidRequest(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.BankError);
    }

    [Fact]
    public async Task ProcessAsync_BankFails_StoresNothingBecauseABankErrorIsNotADecline()
    {
        // Arrange
        _context.BankFails();

        // Act
        await _context.Sut.ProcessAsync(ValidationTestData.ValidRequest(), TestContext.Current.CancellationToken);

        // Assert
        await _context.Repository.DidNotReceiveWithAnyArgs().AddAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ProcessAsync_CallerCancels_PropagatesTheCancellation()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        _context.Bank.AuthorizeAsync(Arg.Any<BankPaymentRequest>(), Arg.Any<CancellationToken>())
            .Returns<Result<BankPaymentResponse>>(_ => throw new OperationCanceledException(cancellation.Token));

        // Act
        var act = () => _context.Sut.ProcessAsync(ValidationTestData.ValidRequest(), cancellation.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}