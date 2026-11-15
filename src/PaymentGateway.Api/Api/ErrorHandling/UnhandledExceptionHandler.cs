using Microsoft.AspNetCore.Diagnostics;

namespace PaymentGateway.Api.ErrorHandling;

/// <summary>
/// The last-resort handler (ADR-17): logs the exception once and answers with a generic RFC 9457 500 that carries
/// the <c>traceId</c> but never the exception's message, type or stack trace, in any environment. Registered after
/// the specific handlers so that they get the first chance.
/// </summary>
public sealed class UnhandledExceptionHandler(ILogger<UnhandledExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // A caller that went away is not a server error.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return true;
        }

        logger.LogError(exception, "Unhandled exception while processing {Method} {Path}",
            httpContext.Request.Method, httpContext.Request.Path);

        await Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "An unexpected error occurred.")
            .ExecuteAsync(httpContext);

        return true;
    }
}