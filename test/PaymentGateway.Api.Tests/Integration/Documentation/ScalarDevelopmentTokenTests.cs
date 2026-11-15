using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PaymentGateway.Api.Tests.Integration.Documentation;

/// <summary>
/// For ease of testing the Scalar page, in Development and in Debug builds only, is pre-loaded with a development token so
/// "Authorize" is already filled in. It must never happen in a Release build or outside Development.
/// </summary>
public class ScalarDevelopmentTokenTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string KeyBase64 = "YS1kZXZlbG9wbWVudC1vbmx5LXNpZ25pbmcta2V5LW9mLTMyLWJ5dGVzIQ==";

    private readonly WebApplicationFactory<Program> _factory;

    public ScalarDevelopmentTokenTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private WebApplicationFactory<Program> WithASigningKey(string environment) =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("Authentication:Schemes:Bearer:ValidAudiences:0", "https://localhost:7092");
            builder.UseSetting("Authentication:Schemes:Bearer:SigningKeys:0:Issuer", "dotnet-user-jwts");
            builder.UseSetting("Authentication:Schemes:Bearer:SigningKeys:0:Value", KeyBase64);
        });

    [Fact]
    public async Task ScalarPage_InADebugBuild_IsPreloadedWithADevelopmentToken()
    {
        // Arrange
        using var factory = WithASigningKey("Development");

        // Act
        var page = await factory.CreateClient().GetStringAsync("/scalar/v1", TestContext.Current.CancellationToken);

        // Assert
#if DEBUG
        page.Should().Contain("eyJ", "the page carries a development token");
#else
        page.Should().NotContain("eyJ", "a Release build never issues a token");
#endif
    }

    [Fact]
    public void DevelopmentScalarToken_IsCompiledIntoDebugBuildsOnly()
    {
        // Arrange
        const string typeName = "PaymentGateway.Api.Documentation.DevelopmentScalarToken";

        // Act
        var exists = typeof(Program).Assembly.GetType(typeName) is not null;

        // Assert
#if DEBUG
        exists.Should().BeTrue();
#else
        exists.Should().BeFalse("the token code must not exist in a Release build");
#endif
    }
}