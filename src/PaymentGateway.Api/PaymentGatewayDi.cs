using FluentValidation;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using PaymentGateway.Api.Documentation;
using PaymentGateway.Api.ErrorHandling;
using PaymentGateway.Api.Options;
using PaymentGateway.Api.Security;
using PaymentGateway.BusinessLogic.Mapping;
using PaymentGateway.BusinessLogic.Services;
using PaymentGateway.BusinessLogic.Validation;
using PaymentGateway.DataLayer.Bank;
using PaymentGateway.DataLayer.Placeholders;
using PaymentGateway.DataLayer.Repositories;

using Serilog;
using Serilog.Formatting.Compact;

namespace PaymentGateway;

/// <summary>
/// The composition root (ADR-12): the one place that knows every layer and wires them together. It has exactly one public
/// method, <see cref="InitialiseDi"/>, first in this file, which <c>Program.cs</c> calls; everything below it is an internal
/// registration for one concern, so reading this file top to bottom shows everything the application is made of. Every
/// mapping from an interface to its implementation is here and nowhere else. The helpers are internal (visible to the
/// tests) so a test can register just the part it needs.
/// </summary>
public static class PaymentGatewayDi
{
    /// <summary>
    /// <b>The root DI initialisation method.</b> Registers everything the application needs, in order, by running each
    /// concern's registration below. This is the only DI entry point: <c>Program.cs</c> calls it once.
    /// </summary>
    public static IServiceCollection InitialiseDi(this IServiceCollection services, IConfiguration configuration) => services
        .AddStructuredLogging(configuration)
        .AddRequestLimits(configuration)
        .AddPaymentMetrics()
        .AddApiAuthentication()
        .AddApiDocumentation()
        .AddErrorHandling()
        .AddPaymentServices()
        .AddBankClient(configuration);

    /// <summary>Registers the payment services: the flow, the in-memory store, the mappings and the validators.</summary>
    internal static IServiceCollection AddPaymentServices(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IPaymentsRepository, PaymentsRepository>();
        services.AddSingleton<IMapping<PostPaymentRequest, BankPaymentRequest>, PostPaymentRequestToBankPaymentRequestMapping>();
        services.AddSingleton<IMapping<ProcessedPaymentModel, PaymentEntity>, ProcessedPaymentToPaymentEntityMapping>();
        services.AddSingleton<IMapping<PaymentEntity, PostPaymentResponse>, PaymentEntityToPostPaymentResponseMapping>();
        services.AddTransient<IPaymentService, PaymentService>();
        services.AddValidatorsFromAssemblyContaining<PostPaymentRequestValidator>(ServiceLifetime.Singleton);

        return services;
    }

    /// <summary>
    /// Registers the bank client as a typed HttpClient whose base address and timeout come from validated configuration
    /// (ADR-12). Bad configuration fails at startup, not on the first payment.
    /// </summary>
    internal static IServiceCollection AddBankClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<BankOptions>()
            .Bind(configuration.GetSection(BankOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient<IBankClient, BankClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<BankOptions>>().Value;
            client.BaseAddress = options.BaseUrl;
            client.Timeout = options.Timeout;
        });

        return services;
    }

    /// <summary>
    /// Registers the metrics implementation. On this branch it is a PLACEHOLDER that records nothing (ADR-15): the bank
    /// client already calls <see cref="IPaymentMetrics"/>, so replacing the no-op with a real one is a one-line change here.
    /// </summary>
    internal static IServiceCollection AddPaymentMetrics(this IServiceCollection services)
    {
        services.AddSingleton<IPaymentMetrics, NoOpPaymentMetrics>();

        return services;
    }

    /// <summary>
    /// JWT bearer authentication plus one policy per scope (ADR-7). A fallback policy makes every endpoint that names no
    /// policy require a signed-in user, so a forgotten requirement fails closed. Issuer, audience and signing keys come from
    /// the <c>Authentication:Schemes:Bearer</c> configuration section, which <c>dotnet user-jwts</c> fills in for development;
    /// with no keys configured no token validates, so the API is closed by default. No token is ever issued here.
    /// </summary>
    internal static IServiceCollection AddApiAuthentication(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICallerIdentity, CallerIdentity>();
        // Keep the claim names exactly as the token has them (sub, scope, merchant_id) instead of mapping sub to a long URI.
        services.AddAuthentication().AddJwtBearer(options => options.MapInboundClaims = false);
        services.AddSingleton<IAuthorizationHandler, ScopeAuthorizationHandler>();
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(PaymentScopes.Create, policy => policy.RequireClaim("sub").AddRequirements(new ScopeRequirement(PaymentScopes.Create)))
            .AddPolicy(PaymentScopes.Read, policy => policy.RequireClaim("sub").AddRequirements(new ScopeRequirement(PaymentScopes.Read)));

        return services;
    }

    /// <summary>
    /// Registers error handling (ADR-12, ADR-17). Binding failures must throw so that the exception handler can answer them
    /// in the same way in every environment; by default the framework only throws them in Development.
    /// </summary>
    internal static IServiceCollection AddErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<BadRequestExceptionHandler>();
        services.AddExceptionHandler<UnhandledExceptionHandler>();
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

        return services;
    }

    /// <summary>
    /// Caps the request body Kestrel will read (ADR-11), so a huge body cannot tie up memory. A body over the limit is
    /// refused with 413 before the endpoint sees it. Rate limiting is a documented gap, not built.
    /// </summary>
    internal static IServiceCollection AddRequestLimits(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RequestLimitsOptions>()
            .Bind(configuration.GetSection(RequestLimitsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<KestrelServerOptions>()
            .Configure<IOptions<RequestLimitsOptions>>((kestrel, limits) =>
                kestrel.Limits.MaxRequestBodySize = limits.Value.MaxRequestBodyBytes);

        return services;
    }

    /// <summary>
    /// Registers Serilog as the logging provider behind <c>ILogger&lt;T&gt;</c> (ADR-11). Levels come from the <c>Serilog</c>
    /// configuration section; events go to the console as JSON with the current trace id. <c>ReadFrom.Services</c> also picks
    /// up any <c>ILogEventSink</c> in the container (how tests capture events); <c>preserveStaticLogger</c> keeps each host's
    /// logger its own, so hosts created side by side do not take each other's events.
    /// </summary>
    internal static IServiceCollection AddStructuredLogging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSerilog((provider, logger) => logger
            .ReadFrom.Configuration(configuration)
            .ReadFrom.Services(provider)
            .WriteTo.Console(new RenderedCompactJsonFormatter()),
            preserveStaticLogger: true);

        return services;
    }
}