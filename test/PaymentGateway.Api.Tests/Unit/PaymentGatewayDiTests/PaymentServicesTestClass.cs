using FluentValidation;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using PaymentGateway;
using PaymentGateway.BusinessLogic.Interfaces;
using PaymentGateway.BusinessLogic.Models;

namespace PaymentGateway.Api.Tests.Unit.PaymentGatewayDiTests;

public class PaymentServicesTestClass : IDisposable
{
    private readonly ServiceProvider _provider;

    // Shared Arrange: build the provider the way the Development host does, validating the wiring (ADR-12).
    public PaymentServicesTestClass()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Bank:BaseUrl"] = "http://localhost:8080" })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<ICallerIdentity>()); // the host supplies it from the request token
        services.AddPaymentMetrics();
        services.AddPaymentServices();
        services.AddBankClient(configuration);

        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    public void Dispose() => _provider.Dispose();

    [Fact]
    public void AddPaymentServices_RegistersTheRepositoryAsASingleton()
    {
        // Arrange: the provider is built in the constructor

        // Act
        var first = _provider.GetRequiredService<IPaymentsRepository>();
        var second = _provider.GetRequiredService<IPaymentsRepository>();

        // Assert
        first.Should().BeSameAs(second);
    }

    [Fact]
    public void AddPaymentServices_RegistersTheRequestValidator()
    {
        // Arrange: the provider is built in the constructor

        // Act
        var validator = _provider.GetService<IValidator<PostPaymentRequest>>();

        // Assert
        validator.Should().NotBeNull();
    }

    [Fact]
    public void AddPaymentServices_RegistersTheSystemClock()
    {
        // Arrange: the provider is built in the constructor

        // Act
        var clock = _provider.GetRequiredService<TimeProvider>();

        // Assert
        clock.Should().BeSameAs(TimeProvider.System);
    }

    [Fact]
    public void AddPaymentServices_RegistersTheRequestToBankRequestMapping()
    {
        // Arrange: the provider is built in the constructor

        // Act
        var mapping = _provider.GetService<IMapping<PostPaymentRequest, BankPaymentRequest>>();

        // Assert
        mapping.Should().NotBeNull();
    }

    [Fact]
    public void AddPaymentServices_RegistersThePaymentService()
    {
        // Arrange: the provider is built in the constructor

        // Act
        var service = _provider.GetService<IPaymentService>();

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void AddPaymentServices_RegistersTheMappingsBetweenPaymentPaymentEntityAndResponse()
    {
        // Arrange: the provider is built in the constructor

        // Act
        var toStored = _provider.GetService<IMapping<ProcessedPaymentModel, PaymentEntity>>();
        var toResponse = _provider.GetService<IMapping<PaymentEntity, PostPaymentResponse>>();

        // Assert
        toStored.Should().NotBeNull();
        toResponse.Should().NotBeNull();
    }
}