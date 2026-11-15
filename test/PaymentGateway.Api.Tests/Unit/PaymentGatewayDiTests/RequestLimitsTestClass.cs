using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using PaymentGateway;

namespace PaymentGateway.Api.Tests.Unit.PaymentGatewayDiTests;

public class RequestLimitsTestClass
{
    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddRequestLimits(configuration);

        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddRequestLimits_ByDefault_CapsTheRequestBodyAtFourKilobytes()
    {
        // Arrange
        using var provider = BuildProvider([]);

        // Act
        var limit = provider.GetRequiredService<IOptions<KestrelServerOptions>>().Value.Limits.MaxRequestBodySize;

        // Assert
        limit.Should().Be(4096);
    }

    [Fact]
    public void AddRequestLimits_TheLimitCanBeConfigured()
    {
        // Arrange
        using var provider = BuildProvider(new() { ["RequestLimits:MaxRequestBodyBytes"] = "1024" });

        // Act
        var limit = provider.GetRequiredService<IOptions<KestrelServerOptions>>().Value.Limits.MaxRequestBodySize;

        // Assert
        limit.Should().Be(1024);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("10000000")]
    public void AddRequestLimits_AnUnreasonableLimit_IsRejectedWhenTheOptionsAreRead(string bytes)
    {
        // Arrange
        using var provider = BuildProvider(new() { ["RequestLimits:MaxRequestBodyBytes"] = bytes });

        // Act
        var read = () => provider.GetRequiredService<IOptions<KestrelServerOptions>>().Value;

        // Assert
        read.Should().Throw<OptionsValidationException>();
    }
}