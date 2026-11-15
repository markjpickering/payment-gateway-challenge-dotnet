namespace PaymentGateway.Api.Security;

public static class TransportSecurityExtensions
{
    /// <summary>
    /// HTTPS everywhere (ADR-7, ADR-11): plain HTTP is redirected to HTTPS, and outside Development browsers are told
    /// to use HTTPS only (HSTS). HSTS is left out of Development so a developer's localhost is not pinned to HTTPS.
    /// </summary>
    public static WebApplication UseTransportSecurity(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        return app;
    }
}