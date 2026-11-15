using Microsoft.Extensions.Time.Testing;

namespace PaymentGateway.Api.Tests.TestSupport;

/// <summary>
/// Shared arrange helpers for the validator tests: a fixed clock, a valid request and a ready validator.
/// </summary>
public static class ValidationTestData
{
    /// <summary>The fixed "now" used by the tests: 14 November 2026, 12:00 UTC.</summary>
    public static readonly DateTimeOffset Now = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    public static PostPaymentRequest ValidRequest() => new()
    {
        CardNumber = "2222405343248877",
        ExpiryMonth = 4,
        ExpiryYear = 2030,
        Currency = "GBP",
        Amount = 100,
        Cvv = "123"
    };

    public static PostPaymentRequestValidator NewValidator() => new(new FakeTimeProvider(Now));
}