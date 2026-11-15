using Scalar.AspNetCore;

namespace PaymentGateway.Api.Documentation;

public static class ApiDocumentationExtensions
{
#if DEBUG
    /// <summary>The OpenAPI document the Scalar page reads: the API's document plus sample values for "Test request".</summary>
    public const string ScalarDocument = "scalar";
#endif

    /// <summary>
    /// Registers the OpenAPI document generator (Microsoft's built-in one; ADR-5): the API's own document, <c>v1</c>. In a Debug
    /// build there is a second document for the Scalar page that adds sample request values (for ease of testing only); the
    /// samples are kept out of the API's own document, and out of Release builds altogether.
    /// </summary>
    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi("v1", options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());
#if DEBUG
        services.AddOpenApi(ScalarDocument, options =>
        {
            options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
            options.AddSchemaTransformer<PaymentRequestExampleTransformer>();
        });
#endif

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
#if DEBUG
            // For ease of testing only, Debug builds in Development: the page reads the document with the sample request values
            // and is pre-loaded with a token. None of this is compiled into a Release build.
            options.WithOpenApiRoutePattern($"/openapi/{ScalarDocument}.json");
            DevelopmentScalarToken.Prefill(options, app.Configuration, app.Services.GetRequiredService<TimeProvider>());
#endif
        }).AllowAnonymous();

        return app;
    }
}