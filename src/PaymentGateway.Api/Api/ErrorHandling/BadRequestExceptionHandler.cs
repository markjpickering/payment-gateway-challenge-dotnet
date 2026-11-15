using Microsoft.AspNetCore.Diagnostics;

namespace PaymentGateway.Api.ErrorHandling;

/// <summary>
/// Turns the framework's request-binding failures into the brief's <c>Rejected</c> answer (ADR-8, ADR-17): a
/// missing, malformed or wrongly typed JSON body is invalid information, so it gets the same 400 as a body that
/// fails validation. Other binding failures (for example 415) keep their own status as <c>ProblemDetails</c>.
/// The exception's message is never returned: it can describe internals.
/// </summary>
public sealed class BadRequestExceptionHandler : IExceptionHandler
{
    private static readonly RejectedPaymentResponse MalformedBody =
        new([new FieldError("body", "The request body is missing, is not valid JSON or has a field of the wrong type.")]);

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException badRequest)
        {
            return false;
        }

        IResult result = badRequest.StatusCode == StatusCodes.Status400BadRequest
            ? Results.Json(MalformedBody, statusCode: StatusCodes.Status400BadRequest)
            : Results.Problem(statusCode: badRequest.StatusCode);

        await result.ExecuteAsync(httpContext);

        return true;
    }
}