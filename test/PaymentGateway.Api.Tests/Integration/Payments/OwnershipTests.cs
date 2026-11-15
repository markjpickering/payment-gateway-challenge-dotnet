using System.Net;
using System.Net.Http.Json;

namespace PaymentGateway.Api.Tests.Integration.Payments;

public class OwnershipTests : IClassFixture<PaymentsApiFactory>
{
    private readonly PaymentsApiFactory _factory;

    public OwnershipTests(PaymentsApiFactory factory)
    {
        _factory = factory;
        _factory.Bank.ClearReceivedCalls();
    }

    private HttpClient ClientFor(string merchantId, string subject = "someone")
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", TestTokens.For(subject: subject, merchantId: merchantId));

        return client;
    }

    private async Task<Guid> PostPaymentAsync(HttpClient client)
    {
        _factory.Bank.Authorizes();
        var response = await client.PostAsJsonAsync(
            "/api/payments", ValidationTestData.ValidRequest(), TestContext.Current.CancellationToken);

        return (await response.Content.ReadFromJsonAsync<PostPaymentResponse>(TestContext.Current.CancellationToken))!.Id;
    }

    [Fact]
    public async Task Get_PaymentOfAnotherMerchant_Returns404()
    {
        // Arrange
        var id = await PostPaymentAsync(ClientFor("merchant-a"));
        var otherMerchant = ClientFor("merchant-b");

        // Act
        var response = await otherMerchant.GetAsync($"/api/payments/{id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_OwnPayment_Returns200()
    {
        // Arrange
        var id = await PostPaymentAsync(ClientFor("merchant-a"));
        var owner = ClientFor("merchant-a");

        // Act
        var response = await owner.GetAsync($"/api/payments/{id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_PaymentCreatedByAnotherUserOfTheSameMerchant_Returns200()
    {
        // Arrange: ownership follows the merchant (on behalf of), not the individual caller (initiated by)
        var id = await PostPaymentAsync(ClientFor("merchant-a", subject: "alice"));
        var colleague = ClientFor("merchant-a", subject: "bob");

        // Act
        var response = await colleague.GetAsync($"/api/payments/{id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}