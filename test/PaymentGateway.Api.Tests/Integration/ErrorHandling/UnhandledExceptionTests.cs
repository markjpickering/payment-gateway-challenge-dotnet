using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

using Microsoft.AspNetCore.Hosting;

namespace PaymentGateway.Api.Tests.Integration.ErrorHandling;

public class UnhandledExceptionTests : IClassFixture<PaymentsApiFactory>
{
    private const string SecretMessage = "secret-internal-detail";

    private readonly PaymentsApiFactory _factory;

    public UnhandledExceptionTests(PaymentsApiFactory factory)
    {
        _factory = factory;
        // Reset the shared substitute first: calling a member that is already scripted to throw would throw here.
        NSubstitute.ClearExtensions.ClearExtensions.ClearSubstitute(_factory.Bank);
        _factory.Bank.AuthorizeAsync(default!, default)
            .ReturnsForAnyArgs<Task<Result<BankPaymentResponse>>>(
                _ => throw new InvalidOperationException(SecretMessage));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Post_WhenAnUnexpectedExceptionIsThrown_Returns500ProblemDetailsWithATraceId(string environment)
    {
        // Arrange
        using var factory = _factory.WithWebHostBuilder(builder => builder.UseEnvironment(environment));
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/payments", ValidationTestData.ValidRequest(), TestContext.Current.CancellationToken);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var body = JsonNode.Parse(text)!;

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        body["status"]!.GetValue<int>().Should().Be(500);
        body["title"]!.GetValue<string>().Should().Be("An unexpected error occurred.");
        body["traceId"]!.GetValue<string>().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Post_WhenAnUnexpectedExceptionIsThrown_RevealsNoMessageOrStackTrace()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/payments", ValidationTestData.ValidRequest(), TestContext.Current.CancellationToken);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        text.Should().NotContain(SecretMessage).And.NotContain("InvalidOperationException").And.NotContain(" at ");
    }
}