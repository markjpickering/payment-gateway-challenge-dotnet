using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using PaymentGateway;

namespace PaymentGateway.Api.Tests.Unit.PaymentGatewayDiTests;

public class BankClientTestClass
{
    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPaymentMetrics();
        services.AddBankClient(configuration);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    [Fact]
    public void AddBankClient_ValidConfiguration_ResolvesTheBankClient()
    {
        // Arrange
        using var provider = BuildProvider(new() { ["Bank:BaseUrl"] = "http://localhost:8080", ["Bank:Timeout"] = "00:00:03" });

        // Act
        var client = provider.GetService<IBankClient>();

        // Assert
        client.Should().NotBeNull();
    }

    [Fact]
    public void AddBankClient_ValidConfiguration_BindsTheBaseUrlAndTimeout()
    {
        // Arrange
        using var provider = BuildProvider(new() { ["Bank:BaseUrl"] = "http://localhost:8080", ["Bank:Timeout"] = "00:00:03" });

        // Act
        var options = provider.GetRequiredService<IOptions<BankOptions>>().Value;

        // Assert
        options.BaseUrl.Should().Be(new Uri("http://localhost:8080"));
        options.Timeout.Should().Be(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void AddBankClient_MissingBaseUrl_FailsOptionsValidation()
    {
        // Arrange
        using var provider = BuildProvider(new() { ["Bank:Timeout"] = "00:00:03" });

        // Act
        var act = () => provider.GetRequiredService<IOptions<BankOptions>>().Value;

        // Assert
        act.Should().Throw<OptionsValidationException>();
    }

    [Theory]
    [InlineData("00:00:00")]
    [InlineData("00:10:00")]
    public void AddBankClient_TimeoutOutOfRange_FailsOptionsValidation(string timeout)
    {
        // Arrange
        using var provider = BuildProvider(new() { ["Bank:BaseUrl"] = "http://localhost:8080", ["Bank:Timeout"] = timeout });

        // Act
        var act = () => provider.GetRequiredService<IOptions<BankOptions>>().Value;

        // Assert
        act.Should().Throw<OptionsValidationException>();
    }
}