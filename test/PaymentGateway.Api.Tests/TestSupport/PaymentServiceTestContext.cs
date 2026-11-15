using Microsoft.Extensions.Logging.Abstractions;

using PaymentGateway.BusinessLogic.Interfaces;
using PaymentGateway.BusinessLogic.Services;

namespace PaymentGateway.Api.Tests.TestSupport;

/// <summary>
/// Shared arrange for the PaymentService tests. Only the I/O edges are substituted (the bank and the
/// repository, where "was it called" is itself the requirement); the validator and mappings are real (ADR-6).
/// </summary>
public sealed class PaymentServiceTestContext
{
    public const string MerchantId = "merchant-1";

    public PaymentServiceTestContext()
    {
        CallerIdentity.OnBehalfOf.Returns(MerchantId);
        Sut = new PaymentService(
            ValidationTestData.NewValidator(),
            new PostPaymentRequestToBankPaymentRequestMapping(),
            new ProcessedPaymentToPaymentEntityMapping(),
            new PaymentEntityToPostPaymentResponseMapping(),
            Bank,
            Repository,
            CallerIdentity,
            NullLogger<PaymentService>.Instance);
    }

    public IBankClient Bank { get; } = Substitute.For<IBankClient>();

    public IPaymentsRepository Repository { get; } = Substitute.For<IPaymentsRepository>();

    public ICallerIdentity CallerIdentity { get; } = Substitute.For<ICallerIdentity>();

    public PaymentService Sut { get; }

    public void BankAuthorizes() => Bank.Authorizes();

    public void BankDeclines() => Bank.Declines();

    public void BankFails() => Bank.Fails();
}