using System.Collections.Concurrent;

namespace PaymentGateway.DataLayer.Repositories;

/// <summary>
/// In-memory payment store (the brief allows this test double). Registered as a singleton, so the storage is
/// thread-safe. It stores and returns entities and nothing else (ADR-26).
/// </summary>
public class PaymentsRepository : IPaymentsRepository
{
    private readonly ConcurrentDictionary<Guid, PaymentEntity> _payments = new();

    public Task AddAsync(PaymentEntity payment, CancellationToken cancellationToken)
    {
        _payments[payment.Id] = payment;

        return Task.CompletedTask;
    }

    public Task<PaymentEntity?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        _payments.TryGetValue(id, out var payment);

        return Task.FromResult(payment);
    }
}