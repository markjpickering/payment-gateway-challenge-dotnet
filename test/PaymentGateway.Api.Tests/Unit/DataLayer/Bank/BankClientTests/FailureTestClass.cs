using System.Net;

namespace PaymentGateway.Api.Tests.Unit.DataLayer.Bank.BankClientTests;

public class FailureTestClass
{
    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task AuthorizeAsync_BankReturnsAnErrorStatus_ReturnsABankError(HttpStatusCode statusCode)
    {
        // Arrange
        var sut = BankClientTestData.NewClient(StubHttpMessageHandler.Responding(statusCode, """{"error_message": "nope"}"""));

        // Act
        var result = await sut.AuthorizeAsync(BankClientTestData.ValidBankRequest(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.BankError);
    }

    [Fact]
    public async Task AuthorizeAsync_NetworkFailure_ReturnsABankError()
    {
        // Arrange
        var sut = BankClientTestData.NewClient(StubHttpMessageHandler.Throwing(new HttpRequestException("connection refused")));

        // Act
        var result = await sut.AuthorizeAsync(BankClientTestData.ValidBankRequest(), TestContext.Current.CancellationToken);

        // Assert
        result.Error!.Kind.Should().Be(ErrorKind.BankError);
    }

    [Fact]
    public async Task AuthorizeAsync_BankTimesOut_ReturnsABankErrorNotACancellation()
    {
        // Arrange
        var timeout = new TaskCanceledException("The request timed out.", new TimeoutException());
        var sut = BankClientTestData.NewClient(StubHttpMessageHandler.Throwing(timeout));

        // Act
        var result = await sut.AuthorizeAsync(BankClientTestData.ValidBankRequest(), TestContext.Current.CancellationToken);

        // Assert
        result.Error!.Kind.Should().Be(ErrorKind.BankError);
    }

    [Theory]
    [InlineData("this is not json")]
    [InlineData("null")]
    [InlineData("{}")]
    public async Task AuthorizeAsync_BankReturnsAnUnreadableBody_ReturnsABankError(string body)
    {
        // Arrange
        var sut = BankClientTestData.NewClient(StubHttpMessageHandler.Responding(HttpStatusCode.OK, body));

        // Act
        var result = await sut.AuthorizeAsync(BankClientTestData.ValidBankRequest(), TestContext.Current.CancellationToken);

        // Assert
        result.Error!.Kind.Should().Be(ErrorKind.BankError);
    }

    [Fact]
    public async Task AuthorizeAsync_AnyFailure_NeverPutsCardDataInTheErrorMessage()
    {
        // Arrange
        var sut = BankClientTestData.NewClient(
            StubHttpMessageHandler.Throwing(new HttpRequestException($"failed for {BankClientTestData.CardNumber}")));

        // Act
        var result = await sut.AuthorizeAsync(BankClientTestData.ValidBankRequest(), TestContext.Current.CancellationToken);

        // Assert
        result.Error!.Message.Should().NotContain(BankClientTestData.CardNumber);
    }

    [Fact]
    public async Task AuthorizeAsync_CallerCancels_PropagatesTheCancellationInsteadOfReturningAnError()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var sut = BankClientTestData.NewClient(
            StubHttpMessageHandler.Throwing(new OperationCanceledException(cancellation.Token)));

        // Act
        var act = () => sut.AuthorizeAsync(BankClientTestData.ValidBankRequest(), cancellation.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}