namespace PaymentGateway.Api.Endpoints;

/// <summary>Turns an expected failure into its HTTP answer, in one place for every endpoint (ADR-17).</summary>
public static class FailureResults
{
    public static IResult ToHttp(this Error error) => error.Kind switch
    {
        ErrorKind.ValidationFailed =>
            Results.Json(new RejectedPaymentResponse(error.FieldErrors), statusCode: StatusCodes.Status400BadRequest),
        _ => Results.Problem(
            statusCode: StatusCodes.Status502BadGateway,
            title: "The acquiring bank could not process the payment.",
            detail: error.Message)
    };
}