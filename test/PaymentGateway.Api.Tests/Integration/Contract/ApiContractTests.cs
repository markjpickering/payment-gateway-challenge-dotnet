using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using NSubstitute.ClearExtensions;

namespace PaymentGateway.Api.Tests.Integration.Contract;

/// <summary>
/// Pins the wire contract from the brief in one place: the exact JSON field names and the status strings. These tests
/// post and read raw JSON, never our own DTO types, so renaming a C# type or property cannot silently change the contract.
/// </summary>
public class ApiContractTests : IClassFixture<PaymentsApiFactory>
{
    // The brief's request fields, as JSON.
    private const string RequestJson =
        """{"cardNumber":"2222405343248877","expiryMonth":4,"expiryYear":2030,"currency":"GBP","amount":1050,"cvv":"123"}""";

    // The brief's response fields: id, status, last four card digits, expiry month and year, currency, amount.
    private static readonly string[] ResponseFields =
        ["id", "status", "cardNumberLastFour", "expiryMonth", "expiryYear", "currency", "amount"];

    private readonly PaymentsApiFactory _factory;

    public ApiContractTests(PaymentsApiFactory factory)
    {
        _factory = factory;
        _factory.Bank.ClearSubstitute();
    }

    private async Task<(HttpResponseMessage Response, JsonObject Body)> PostAsync(string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _factory.CreateClient().PostAsync("/api/payments", content, TestContext.Current.CancellationToken);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!.AsObject();

        return (response, body);
    }

    [Fact]
    public async Task Post_BriefsRequestFields_AreAccepted()
    {
        // Arrange
        _factory.Bank.Authorizes();

        // Act
        var (response, _) = await PostAsync(RequestJson);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(true, "Authorized")]
    [InlineData(false, "Declined")]
    public async Task Post_Processed_ReturnsExactlyTheBriefsFieldsWithTheStatusAsAString(bool authorized, string expectedStatus)
    {
        // Arrange
        if (authorized) { _factory.Bank.Authorizes(); } else { _factory.Bank.Declines(); }

        // Act
        var (_, body) = await PostAsync(RequestJson);

        // Assert
        body.Select(p => p.Key).Should().BeEquivalentTo(ResponseFields);
        body["status"]!.GetValue<string>().Should().Be(expectedStatus);
    }

    [Fact]
    public async Task Post_Processed_ReturnsEachFieldAsTheExpectedJsonType()
    {
        // Arrange
        _factory.Bank.Authorizes();

        // Act
        var (_, body) = await PostAsync(RequestJson);

        // Assert
        Guid.TryParse(body["id"]!.GetValue<string>(), out _).Should().BeTrue();
        body["cardNumberLastFour"]!.GetValueKind().Should().Be(JsonValueKind.String);
        body["cardNumberLastFour"]!.GetValue<string>().Should().Be("8877");
        body["expiryMonth"]!.GetValue<int>().Should().Be(4);
        body["expiryYear"]!.GetValue<int>().Should().Be(2030);
        body["currency"]!.GetValue<string>().Should().Be("GBP");
        body["amount"]!.GetValue<int>().Should().Be(1050);
    }

    [Fact]
    public async Task Get_ReturnsTheSameFieldsAsPost()
    {
        // Arrange
        _factory.Bank.Authorizes();
        var (_, posted) = await PostAsync(RequestJson);

        // Act
        var json = await _factory.CreateClient().GetStringAsync($"/api/payments/{posted["id"]!.GetValue<string>()}", TestContext.Current.CancellationToken);
        var retrieved = JsonNode.Parse(json)!.AsObject();

        // Assert
        retrieved.Select(p => p.Key).Should().BeEquivalentTo(ResponseFields);
        retrieved.ToJsonString().Should().Be(posted.ToJsonString());
    }

    [Fact]
    public async Task Post_InvalidRequest_ReturnsARejectedStatusAndNeverTheBriefsPaymentFields()
    {
        // Arrange
        var invalid = RequestJson.Replace("\"cardNumber\":\"2222405343248877\"", "\"cardNumber\":\"123\"");

        // Act
        var (response, body) = await PostAsync(invalid);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body["status"]!.GetValue<string>().Should().Be("Rejected");
        body.ContainsKey("id").Should().BeFalse("no payment was created");
    }

    [Theory]
    [InlineData("cardNumber")]
    [InlineData("expiryMonth")]
    [InlineData("expiryYear")]
    [InlineData("currency")]
    [InlineData("amount")]
    [InlineData("cvv")]
    public async Task Post_AnyBriefsFieldMissing_IsRejected(string field)
    {
        // Arrange
        var request = JsonNode.Parse(RequestJson)!.AsObject();
        request.Remove(field);

        // Act
        var (response, body) = await PostAsync(request.ToJsonString());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body["status"]!.GetValue<string>().Should().Be("Rejected");
    }
}