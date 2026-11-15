using Scalar.AspNetCore;

namespace PaymentGateway.Api.Documentation;

public static class ApiDocumentationExtensions
{
    /// <summary>
    /// Registers the OpenAPI document generator (Microsoft's built-in one; ADR-5).
    /// </summary>
    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

        return services;
    }

    /// <summary>
    /// Serves the OpenAPI document and the Scalar UI, in the Development environment only.
    /// </summary>
    public static WebApplication MapApiDocumentation(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return app;
        }

        app.MapOpenApi().AllowAnonymous();
        app.MapScalarApiReference().AllowAnonymous();

        return app;
    }
}