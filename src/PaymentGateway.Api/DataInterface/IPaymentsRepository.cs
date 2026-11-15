namespace PaymentGateway.DataInterface;

/// <summary>
/// Storage for processed payments (ADR-26): it only adds and reads <see cref="PaymentEntity"/> entities by id. It has no
/// business logic and does not know who is asking: whether a caller may see a payment (ownership) is decided by the service.
/// Async because any real store is I/O (ADR-16).
/// </summary>
public interface IPaymentsRepository
{
    Task AddAsync(PaymentEntity payment, CancellationToken cancellationToken);

    Task<PaymentEntity?> GetAsync(Guid id, CancellationToken cancellationToken);
}