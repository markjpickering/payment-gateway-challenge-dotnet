namespace PaymentGateway.Api.Tests.TestSupport;

/// <summary>Scripts the three outcomes of a substituted <see cref="IBankClient"/>.</summary>
public static class BankClientSubstituteExtensions
{
    public static void Authorizes(this IBankClient bank) => bank.ReturnsFor(Result<BankPaymentResponse>.Success(
        new BankPaymentResponse { Authorized = true, AuthorizationCode = "0bb07405-6d44-4b50-a14f-7ae0beff13ad" }));

    public static void Declines(this IBankClient bank) => bank.ReturnsFor(Result<BankPaymentResponse>.Success(
        new BankPaymentResponse { Authorized = false }));

    public static void Fails(this IBankClient bank) => bank.ReturnsFor(Result<BankPaymentResponse>.Failure(
        new Error(ErrorKind.BankError, "The bank could not be reached.")));

    private static void ReturnsFor(this IBankClient bank, Result<BankPaymentResponse> result) =>
        bank.AuthorizeAsync(Arg.Any<BankPaymentRequest>(), Arg.Any<CancellationToken>()).Returns(result);
}