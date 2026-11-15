using PaymentGateway.DataLayer.Repositories;

namespace PaymentGateway.Api.Tests.Unit.DataLayer.Repositories;

/// <summary>
/// The repository only stores and retrieves (ADR-26): it has no notion of who may see a payment, so every test here is
/// about storage alone. Ownership is a business rule and is tested on <see cref="PaymentService"/>.
/// </summary>
public class PaymentsRepositoryTests
{
    // Shared Arrange for every test: a new, empty repository (xUnit creates a new instance per test).
    private readonly PaymentsRepository _sut = new();

    [Fact]
    public async Task GetAsync_UnknownId_ReturnsNull()
    {
        // Arrange
        var unknownId = Guid.NewGuid();

        // Act
        var result = await _sut.GetAsync(unknownId, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task AddAsync_ThenGetAsync_ReturnsThePaymentEntity()
    {
        // Arrange
        var payment = NewPayment();
        await _sut.AddAsync(payment, TestContext.Current.CancellationToken);

        // Act
        var result = await _sut.GetAsync(payment.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(payment);
    }

    [Fact]
    public async Task GetAsync_ReturnsThePaymentOfAnyMerchant_BecauseOwnershipIsNotTheStoresConcern()
    {
        // Arrange
        var payment = NewPayment() with { MerchantId = "merchant-2" };
        await _sut.AddAsync(payment, TestContext.Current.CancellationToken);

        // Act
        var result = await _sut.GetAsync(payment.Id, TestContext.Current.CancellationToken);

        // Assert
        result!.MerchantId.Should().Be("merchant-2");
    }

    [Fact]
    public async Task AddAsync_LastFourWithLeadingZero_PreservesTheLeadingZero()
    {
        // Arrange
        var payment = NewPayment() with { CardNumberLastFour = "0042" };
        await _sut.AddAsync(payment, TestContext.Current.CancellationToken);

        // Act
        var result = await _sut.GetAsync(payment.Id, TestContext.Current.CancellationToken);

        // Assert
        result!.CardNumberLastFour.Should().Be("0042");
    }

    [Fact]
    public async Task AddAsync_ManyConcurrentAdds_StoresEveryPayment()
    {
        // Arrange
        var payments = Enumerable.Range(0, 1000).Select(_ => NewPayment()).ToList();

        // Act
        await Parallel.ForEachAsync(payments, async (payment, ct) => await _sut.AddAsync(payment, ct));

        // Assert
        foreach (var payment in payments)
        {
            var result = await _sut.GetAsync(payment.Id, TestContext.Current.CancellationToken);
            result.Should().NotBeNull();
        }
    }

    private static PaymentEntity NewPayment() => new(
        Guid.NewGuid(), "merchant-1", PaymentStatus.Authorized, "8877", 12, 2030, "GBP", 100);
}