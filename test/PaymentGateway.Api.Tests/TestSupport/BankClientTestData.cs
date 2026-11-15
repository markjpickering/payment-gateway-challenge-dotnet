using Microsoft.Extensions.Logging.Abstractions;

namespace PaymentGateway.Api.Tests.TestSupport;

/// <summary>
/// Shared arrange helpers for the bank client tests: a client wired to a stub handler and a valid bank request.
/// </summary>
public static class BankClientTestData
{
    public const string CardNumber = "2222405343248877";

    public static BankClient NewClient(StubHttpMessageHandler handler, IPaymentMetrics? metrics = null, TimeProvider? clock = null) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://bank.test") }, NullLogger<BankClient>.Instance,
            metrics ?? Substitute.For<IPaymentMetrics>(), clock ?? TimeProvider.System);

    public static BankPaymentRequest ValidBankRequest() => new()
    {
        CardNumber = CardNumber,
        ExpiryDate = "04/2030",
        Currency = "GBP",
        Amount = 100,
        Cvv = "123"
    };
}