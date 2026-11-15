using Scalar.AspNetCore;

namespace PaymentGateway.Api.Documentation;

public static class ApiDocumentationExtensions
{
    /// <summary>The OpenAPI document the Scalar page reads: the API's document plus sample values for "Test request".</summary>
    public const string ScalarDocument = "scalar";

    /// <summary>
    /// Registers the OpenAPI document generator (Microsoft's built-in one; ADR-5): the API's own document, <c>v1</c>, and a
    /// second one for the Scalar page that adds the sample request values. The samples are a Scalar convenience, so they
    /// are kept out of the API's own document.
    /// </summary>
    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi("v1", options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());
        services.AddOpenApi(ScalarDocument, options =>
        {
            options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
            options.AddSchemaTransformer<PaymentRequestExampleTransformer>();
        });

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
        app.MapScalarApiReference(options =>
        {
            options.WithOpenApiRoutePattern($"/openapi/{ScalarDocument}.json");
#if DEBUG
            // For ease of testing only: a Debug build, in Development, pre-loads the page with a token. Not in Release.
            DevelopmentScalarToken.Prefill(options, app.Configuration, app.Services.GetRequiredService<TimeProvider>());
#endif
        }).AllowAnonymous();

        return app;
    }
}