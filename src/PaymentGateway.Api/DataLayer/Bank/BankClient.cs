using System.Text.Json;

namespace PaymentGateway.DataLayer.Bank;

/// <summary>
/// Adapter pattern: adapts the acquiring bank's HTTP API to <see cref="IBankClient"/>. It is the one place
/// that catches transport exceptions, and turns every one of them into a bank-error <see cref="Result{T}"/>,
/// so callers never see HTTP exceptions (ADR-9, ADR-17). Caller cancellation is not an error and propagates.
/// </summary>
public sealed class BankClient(
    HttpClient httpClient, ILogger<BankClient> logger, IPaymentMetrics metrics, TimeProvider clock) : IBankClient
{
    public async Task<Result<BankPaymentResponse>> AuthorizeAsync(BankPaymentRequest request, CancellationToken cancellationToken)
    {
        var started = clock.GetTimestamp();
        var result = await CallAsync(request, cancellationToken);
        metrics.BankRequest(OutcomeOf(result), clock.GetElapsedTime(started));

        return result;
    }

    private static BankOutcome OutcomeOf(Result<BankPaymentResponse> result) => result switch
    {
        { IsSuccess: false } => BankOutcome.Error,
        { Value.Authorized: true } => BankOutcome.Authorized,
        _ => BankOutcome.Declined
    };

    private async Task<Result<BankPaymentResponse>> CallAsync(BankPaymentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync("payments", request, cancellationToken);

            return await ReadAsync(response, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            return Failure("The bank could not be reached.", exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure("The bank did not respond in time.", exception);
        }
        catch (JsonException exception)
        {
            return Failure("The bank returned an unreadable response.", exception);
        }
    }

    private async Task<Result<BankPaymentResponse>> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            return Failure($"The bank returned status {(int)response.StatusCode}.");
        }

        var body = await response.Content.ReadFromJsonAsync<BankPaymentResponse>(cancellationToken);

        return body is null
            ? Failure("The bank returned an empty response.")
            : Result<BankPaymentResponse>.Success(body);
    }

    // The returned message is generic and safe to show to a merchant; the detail goes to the log only.
    private Result<BankPaymentResponse> Failure(string message, Exception? exception = null)
    {
        logger.LogWarning(exception, "Bank call failed: {Reason}", message);

        return Result<BankPaymentResponse>.Failure(new Error(ErrorKind.BankError, message));
    }
}