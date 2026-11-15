using PaymentGateway;
using PaymentGateway.Api.Documentation;
using PaymentGateway.Api.Endpoints;
using PaymentGateway.Api.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.InitialiseDi(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.MapApiDocumentation();

app.UseTransportSecurity();

app.UseAuthentication();
app.UseAuthorization();

app.MapPaymentEndpoints();

app.Run();