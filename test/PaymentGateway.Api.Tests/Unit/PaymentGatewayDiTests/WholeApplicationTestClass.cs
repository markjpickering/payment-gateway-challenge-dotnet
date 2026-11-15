using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using PaymentGateway.BusinessLogic.Interfaces;

namespace PaymentGateway.Api.Tests.Unit.PaymentGatewayDiTests;

public class WholeApplicationTestClass
{
    private static ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Bank:BaseUrl"] = "http://localhost:8080" })
            .Build();
        var services = new ServiceCollection();
        services.InitialiseDi(configuration);

        return services.BuildServiceProvider();
    }

    [Fact]
    public void InitialiseDi_RegistersEverythingTheEndpointsNeed()
    {
        // Arrange
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        // Act
        var service = scope.ServiceProvider.GetService<IPaymentService>();
        var identity = scope.ServiceProvider.GetService<ICallerIdentity>();

        // Assert
        service.Should().NotBeNull();
        identity.Should().NotBeNull();
    }

    [Fact]
    public void InitialiseDi_RegistersTheBankClientAndTheMetricsItCalls()
    {
        // Arrange
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        // Act
        var bank = scope.ServiceProvider.GetService<IBankClient>();
        var metrics = scope.ServiceProvider.GetService<IPaymentMetrics>();

        // Assert
        bank.Should().NotBeNull();
        metrics.Should().NotBeNull();
    }
}