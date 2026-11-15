using System.Text.Json.Nodes;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using PaymentGateway.ApiInterface.Dtos.Requests;

namespace PaymentGateway.Api.Documentation;

/// <summary>
/// Gives the payment request an example that is a valid payment, so Scalar's "Test request" panel is pre-filled and Send works
/// as it stands. The expiry is a few years ahead of the clock, so the example does not go stale.
/// </summary>
public sealed class PaymentRequestExampleTransformer(TimeProvider clock) : IOpenApiSchemaTransformer
{
    private const int YearsAhead = 3;

    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.JsonTypeInfo.Type == typeof(PostPaymentRequest))
        {
            schema.Example = new JsonObject
            {
                ["cardNumber"] = "2222405343248877", // ends in an odd digit: the bank simulator authorizes it
                ["expiryMonth"] = 4,
                ["expiryYear"] = clock.GetUtcNow().Year + YearsAhead,
                ["currency"] = "GBP",
                ["amount"] = 100,
                ["cvv"] = "123"
            };
        }

        return Task.CompletedTask;
    }
}