using System.Net;
using System.Text.Json.Nodes;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PaymentGateway.Api.Tests.Integration.Documentation;

public class ApiDocumentationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    // The factory runs in the Development environment by default and is shared through the class fixture.
    public ApiDocumentationTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task OpenApiDocument_InDevelopment_DescribesTheGetPaymentEndpoint()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        var document = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        document.Should().Contain("/api/payments/{id}");
    }

    [Fact]
    public async Task OpenApiDocument_InDevelopment_DeclaresTheBearerSecurityScheme()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var document = await client.GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        var scheme = JsonNode.Parse(document)!["components"]!["securitySchemes"]!["Bearer"]!;

        // Assert
        scheme["type"]!.GetValue<string>().Should().Be("http");
        scheme["scheme"]!.GetValue<string>().Should().Be("bearer");
        JsonNode.Parse(document)!["security"]!.AsArray().Should().NotBeEmpty();
    }

    [Fact]
    public async Task ApiDocument_PostPaymentRequestSchema_CarriesNoSampleValues()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var document = await client.GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        var schema = JsonNode.Parse(document)!["components"]!["schemas"]!["PostPaymentRequest"]!.AsObject();

        // Assert
        schema.ContainsKey("example").Should().BeFalse("sample values are a Scalar convenience, not part of the API's contract");
        schema.ContainsKey("examples").Should().BeFalse();
    }

#if !DEBUG
    [Fact]
    public async Task ScalarSampleDocument_InAReleaseBuild_DoesNotExist()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var sampleDocument = await client.GetAsync("/openapi/scalar.json", TestContext.Current.CancellationToken);
        var page = await client.GetStringAsync("/scalar/v1", TestContext.Current.CancellationToken);

        // Assert
        sampleDocument.StatusCode.Should().Be(HttpStatusCode.NotFound, "sample request values are compiled into Debug builds only");
        page.Should().Contain("openapi/v1.json").And.NotContain("scalar.json");
    }
#endif

    [Fact]
    public async Task ScalarUi_InDevelopment_IsServed()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/scalar/v1", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar/v1")]
    public async Task Documentation_OutsideDevelopment_IsNotExposed(string path)
    {
        // Arrange
        using var productionFactory = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        var client = productionFactory.CreateClient();

        // Act
        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        // Assert: an anonymous caller gets 401 for any path that is not a public one (the fallback policy), never the document
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Unauthorized);
    }
}