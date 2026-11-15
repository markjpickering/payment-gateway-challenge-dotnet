using FluentValidation;
using FluentValidation.Results;

namespace PaymentGateway.BusinessLogic.Services;

/// <summary>
/// The payment flow: validate, call the bank, store (ADR-1). An invalid request never reaches the bank or the
/// store, and a bank error is not stored because it is not a decline (ADR-9). Cross-cutting concerns such as
/// idempotency and auditing belong in decorators around this service, not in it (ADR-25).
/// </summary>
public sealed class PaymentService(
    IValidator<PostPaymentRequest> validator,
    IMapping<PostPaymentRequest, BankPaymentRequest> bankRequestMapping,
    IMapping<ProcessedPaymentModel, PaymentEntity> entityMapping,
    IMapping<PaymentEntity, PostPaymentResponse> responseMapping,
    IBankClient bankClient,
    IPaymentsRepository repository,
    ICallerIdentity caller,
    ILogger<PaymentService> logger) : IPaymentService
{
    public async Task<Result<PostPaymentResponse>> ProcessAsync(PostPaymentRequest request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Rejected(validation);
        }

        var bankResult = await bankClient.AuthorizeAsync(bankRequestMapping.Map(request), cancellationToken);
        if (!bankResult.IsSuccess)
        {
            return Result<PostPaymentResponse>.Failure(bankResult.Error);
        }

        var payment = new ProcessedPaymentModel(Guid.NewGuid(), caller.OnBehalfOf, StatusOf(bankResult.Value), request);
        var entity = entityMapping.Map(payment);
        await repository.AddAsync(entity, cancellationToken);
        logger.LogInformation("Payment {PaymentId} processed with status {Status}", payment.Id, payment.Status);

        return Result<PostPaymentResponse>.Success(responseMapping.Map(entity));
    }

    // Ownership is a business rule, so it is decided here and not in the store (ADR-26): another merchant's payment
    // is reported as not found, exactly like an unknown id.
    public async Task<PostPaymentResponse?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await repository.GetAsync(id, cancellationToken);

        return entity is not null && entity.MerchantId == caller.OnBehalfOf ? responseMapping.Map(entity) : null;
    }

    private static PaymentStatus StatusOf(BankPaymentResponse bankResponse) =>
        bankResponse.Authorized ? PaymentStatus.Authorized : PaymentStatus.Declined;

    private static Result<PostPaymentResponse> Rejected(ValidationResult validation) =>
        Result<PostPaymentResponse>.Failure(new Error(ErrorKind.ValidationFailed, "The payment request is not valid.")
        {
            FieldErrors = [.. validation.Errors.Select(e => new FieldError(e.PropertyName, e.ErrorMessage))]
        });
}