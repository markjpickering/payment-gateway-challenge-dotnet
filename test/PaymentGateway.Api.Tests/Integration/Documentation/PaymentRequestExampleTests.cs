using System.Net;
using System.Text;
using System.Text.Json.Nodes;

using NSubstitute.ClearExtensions;

namespace PaymentGateway.Api.Tests.Integration.Documentation;

/// <summary>
/// Scalar's "Test request" panel is pre-filled from an example, so the example must be a payment the API accepts as it stands
/// (a reviewer should be able to press Send) and must stay valid as the calendar moves on. The sample values belong to the
/// Scalar interface, not to the API: they live in a second OpenAPI document that only the Scalar page reads, and the API's
/// own document (<c>/openapi/v1.json</c>) carries none.
/// </summary>
public class PaymentRequestExampleTests : IClassFixture<PaymentsApiFactory>
{
    private readonly PaymentsApiFactory _factory;

    public PaymentRequestExampleTests(PaymentsApiFactory factory)
    {
        _factory = factory;
        _factory.Bank.ClearSubstitute();
    }

    private async Task<JsonObject> ExampleAsync()
    {
        var document = await _factory.CreateClient().GetStringAsync("/openapi/scalar.json", TestContext.Current.CancellationToken);
        var example = JsonNode.Parse(document)!["components"]!["schemas"]!["PostPaymentRequest"]!["example"];

        return example!.AsObject();
    }

    [Fact]
    public async Task ApiDocument_PostPaymentRequestSchema_CarriesNoSampleValues()
    {
        // Arrange: done in the constructor

        // Act
        var document = await _factory.CreateClient().GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        var schema = JsonNode.Parse(document)!["components"]!["schemas"]!["PostPaymentRequest"]!.AsObject();

        // Assert
        schema.ContainsKey("example").Should().BeFalse("the sample values are a Scalar convenience, not part of the API's contract");
    }

    [Fact]
    public async Task ScalarUi_ReadsTheDocumentThatCarriesTheExample()
    {
        // Arrange: done in the constructor

        // Act
        var page = await _factory.CreateClient().GetStringAsync("/scalar/v1", TestContext.Current.CancellationToken);

        // Assert
        page.Should().Contain("openapi/scalar.json");
    }

    [Fact]
    public async Task OpenApiDocument_PostPaymentRequestSchema_HasAnExampleWithEveryField()
    {
        // Arrange: done in the constructor

        // Act
        var example = await ExampleAsync();

        // Assert
        example.Select(p => p.Key).Should().BeEquivalentTo("cardNumber", "expiryMonth", "expiryYear", "currency", "amount", "cvv");
    }

    [Fact]
    public async Task OpenApiDocument_PostPaymentRequestExample_IsAcceptedByTheApiAsItStands()
    {
        // Arrange
        _factory.Bank.Authorizes();
        var example = await ExampleAsync();
        using var content = new StringContent(example.ToJsonString(), Encoding.UTF8, "application/json");

        // Act
        var response = await _factory.CreateClient().PostAsync("/api/payments", content, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OpenApiDocument_PostPaymentRequestExample_ExpiresInTheFuture()
    {
        // Arrange: done in the constructor

        // Act
        var example = await ExampleAsync();

        // Assert
        example["expiryYear"]!.GetValue<int>().Should().BeGreaterThan(DateTime.UtcNow.Year);
    }
}