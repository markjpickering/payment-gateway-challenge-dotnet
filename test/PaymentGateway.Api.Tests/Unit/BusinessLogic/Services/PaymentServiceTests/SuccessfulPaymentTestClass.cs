using System.Text.Json;

namespace PaymentGateway.Api.Tests.Unit.BusinessLogic.Services.PaymentServiceTests;

public class SuccessfulPaymentTestClass
{
    // Shared Arrange for every test: the service with a substituted bank and repository.
    private readonly PaymentServiceTestContext _context = new();

    [Fact]
    public async Task ProcessAsync_BankAuthorizes_ReturnsAnAuthorizedPaymentWithTheRequestDetails()
    {
        // Arrange
        _context.BankAuthorizes();
        var request = ValidationTestData.ValidRequest();

        // Act
        var result = await _context.Sut.ProcessAsync(request, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().NotBeEmpty();
        result.Value!.Status.Should().Be(PaymentStatus.Authorized);
        result.Value!.CardNumberLastFour.Should().Be("8877");
        result.Value!.ExpiryMonth.Should().Be(request.ExpiryMonth);
        result.Value!.ExpiryYear.Should().Be(request.ExpiryYear);
        result.Value!.Currency.Should().Be(request.Currency);
        result.Value!.Amount.Should().Be(request.Amount);
    }

    [Fact]
    public async Task ProcessAsync_BankDeclines_ReturnsADeclinedPayment()
    {
        // Arrange
        _context.BankDeclines();

        // Act
        var result = await _context.Sut.ProcessAsync(ValidationTestData.ValidRequest(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(PaymentStatus.Declined);
    }

    [Theory]
    [InlineData(true, PaymentStatus.Authorized)]
    [InlineData(false, PaymentStatus.Declined)]
    public async Task ProcessAsync_BankAnswers_StoresTheReturnedPayment(bool authorized, PaymentStatus expectedStatus)
    {
        // Arrange
        if (authorized) { _context.BankAuthorizes(); } else { _context.BankDeclines(); }

        // Act
        var result = await _context.Sut.ProcessAsync(ValidationTestData.ValidRequest(), TestContext.Current.CancellationToken);

        // Assert
        await _context.Repository.Received(1).AddAsync(
            Arg.Is<PaymentEntity>(p => p.Id == result.Value!.Id && p.Status == expectedStatus
                && p.MerchantId == PaymentServiceTestContext.MerchantId && p.CardNumberLastFour == "8877"),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ProcessAsync_ValidRequest_SendsTheMappedRequestToTheBankOnce()
    {
        // Arrange
        _context.BankAuthorizes();
        var request = ValidationTestData.ValidRequest();

        // Act
        await _context.Sut.ProcessAsync(request, TestContext.Current.CancellationToken);

        // Assert
        await _context.Bank.Received(1).AuthorizeAsync(
            Arg.Is<BankPaymentRequest>(r => r.CardNumber == request.CardNumber && r.ExpiryDate == "04/2030" && r.Cvv == request.Cvv),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ProcessAsync_ValidRequest_NeverReturnsTheFullCardNumberOrCvv()
    {
        // Arrange
        _context.BankAuthorizes();
        var request = ValidationTestData.ValidRequest();

        // Act
        var result = await _context.Sut.ProcessAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var json = JsonSerializer.Serialize(result.Value);
        json.Should().NotContain(request.CardNumber).And.NotContain(request.Cvv);
    }
}