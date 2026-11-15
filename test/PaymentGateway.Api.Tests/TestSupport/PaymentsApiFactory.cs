using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

using Serilog.Core;

namespace PaymentGateway.Api.Tests.TestSupport;

/// <summary>
/// The real application with only the bank replaced by a substitute and the token-validation keys swapped for
/// the tests' own (ADR-12). Everything else, including the validators, services, repository, endpoints and
/// authorization policies, is the production wiring.
/// </summary>
public sealed class PaymentsApiFactory : WebApplicationFactory<Program>
{
    public IBankClient Bank { get; } = Substitute.For<IBankClient>();

    public CapturingLogSink Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IBankClient>();
            services.AddSingleton(Bank);
            services.AddSingleton<ILogEventSink>(Logs);
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = TestTokens.Issuer,
                    ValidAudience = TestTokens.Audience,
                    IssuerSigningKey = TestTokens.SigningKey
                });
        });

    /// <summary>Clients are signed in with every scope by default; authentication tests replace the header.</summary>
    protected override void ConfigureClient(HttpClient client) =>
        client.DefaultRequestHeaders.Authorization = new("Bearer", TestTokens.For());
}