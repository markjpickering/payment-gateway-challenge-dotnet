using PaymentGateway.Api.Security;

namespace PaymentGateway.Api.Endpoints;

/// <summary>
/// The payment endpoints (ADR-2). They stay thin: bind, call a service, map the result to HTTP.
/// </summary>
public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var payments = app.MapGroup("/api/payments").WithTags("Payments");

        payments.MapPost("/", ProcessPaymentAsync).RequireAuthorization(PaymentScopes.Create);
        payments.MapGet("/{id:guid}", GetPaymentAsync).RequireAuthorization(PaymentScopes.Read);

        return app;
    }

    private static async Task<IResult> ProcessPaymentAsync(
        PostPaymentRequest request, IPaymentService service, CancellationToken cancellationToken)
    {
        var result = await service.ProcessAsync(request, cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttp();
    }

    private static async Task<IResult> GetPaymentAsync(
        Guid id, IPaymentService service, CancellationToken cancellationToken)
    {
        var payment = await service.GetAsync(id, cancellationToken);

        return payment is null ? Results.NotFound() : Results.Ok(payment);
    }
}